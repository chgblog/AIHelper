// Copyright (C) 2026 chgblog
// SPDX-License-Identifier: GPL-3.0
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AIHelper.Models;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Newtonsoft.Json.Linq;

namespace AIHelper.Services
{
    public class LinkItem
    {
        public string Href { get; set; }
        public string Title { get; set; }
    }

    /// <summary>
    /// 自动访问执行引擎：负责视口链接抓取、子页面真人滚动浏览、返回继续遍历、翻屏翻页与定时刷新重访
    /// </summary>
    public class AutoVisitService
    {
        private static readonly Lazy<AutoVisitService> _instance =
            new Lazy<AutoVisitService>(() => new AutoVisitService());

        public static AutoVisitService Instance => _instance.Value;

        private readonly Random _random = new Random();
        private CancellationTokenSource _cts;

        public bool IsRunning { get; private set; }

        private AutoVisitService() { }

        /// <summary>
        /// 判断当前 URL 是否匹配配置的某条已启用的自动访问网址
        /// </summary>
        public AutoVisitConfig FindMatchingConfig(string currentUrl, IEnumerable<AutoVisitConfig> configs)
        {
            if (string.IsNullOrWhiteSpace(currentUrl) || configs == null) return null;
            string normCurrent = AutoVisitHistoryService.NormalizeUrl(currentUrl);

            foreach (var cfg in configs)
            {
                if (cfg == null || !cfg.IsEnabled || string.IsNullOrWhiteSpace(cfg.Url)) continue;

                string normCfg = AutoVisitHistoryService.NormalizeUrl(cfg.Url);
                if (string.Equals(normCurrent, normCfg, StringComparison.OrdinalIgnoreCase))
                {
                    return cfg;
                }

                // 支持主路径匹配（例如当前网址包含查询参数或翻页路径）
                try
                {
                    if (Uri.TryCreate(currentUrl, UriKind.Absolute, out Uri curUri) &&
                        Uri.TryCreate(cfg.Url, UriKind.Absolute, out Uri cfgUri))
                    {
                        if (string.Equals(curUri.Host, cfgUri.Host, StringComparison.OrdinalIgnoreCase))
                        {
                            string curPath = curUri.AbsolutePath.TrimEnd('/');
                            string cfgPath = cfgUri.AbsolutePath.TrimEnd('/');
                            if (curPath.Equals(cfgPath, StringComparison.OrdinalIgnoreCase) ||
                                curPath.StartsWith(cfgPath + "/", StringComparison.OrdinalIgnoreCase) ||
                                string.IsNullOrEmpty(cfgPath))
                            {
                                return cfg;
                            }
                        }
                    }
                }
                catch { }

                if (currentUrl.StartsWith(cfg.Url.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
                {
                    return cfg;
                }
            }

            return null;
        }

        /// <summary>
        /// 从候选链接列表中按顺序选取第一个尚未访问、非当前主页面自身且未被排除规则过滤的链接
        /// </summary>
        public LinkItem SelectFirstUnvisitedLink(
            IEnumerable<LinkItem> screenLinks,
            string currentMainUrl,
            IEnumerable<string> excludedList)
        {
            if (screenLinks == null) return null;
            return screenLinks.FirstOrDefault(l =>
                l != null &&
                !string.IsNullOrWhiteSpace(l.Href) &&
                !IsSamePage(l.Href, currentMainUrl) &&
                !IsExcludedUrl(l.Href, excludedList) &&
                !AutoVisitHistoryService.Instance.HasVisitedInLast24Hours(l.Href));
        }

        /// <summary>
        /// 启动自动访问流程
        /// </summary>
        public void Start(
            WebView2 webView,
            AutoVisitConfig config,
            AppSettings settings,
            Action<string> updateStatus,
            Func<bool> isSimulateEnabled)
        {
            Stop();

            _cts = new CancellationTokenSource();
            IsRunning = true;
            _ = RunAutoVisitLoopAsync(webView, config, settings, updateStatus, isSimulateEnabled, _cts.Token);
        }

        /// <summary>
        /// 停止自动访问流程
        /// </summary>
        public void Stop()
        {
            if (_cts != null)
            {
                try
                {
                    _cts.Cancel();
                    _cts.Dispose();
                }
                catch { }
                _cts = null;
            }
            IsRunning = false;
        }

        private async Task RunAutoVisitLoopAsync(
            WebView2 webView,
            AutoVisitConfig config,
            AppSettings settings,
            Action<string> updateStatus,
            Func<bool> isSimulateEnabled,
            CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested && isSimulateEnabled() && IsRunning)
                {
                    int roundVisitedCount = 0;
                    string mainUrl = config.Url;

                    // 确保处于主页面并重置到首屏顶部
                    await EnsureOnPageAsync(webView, mainUrl, updateStatus, ct);
                    await ScrollToTopAsync(webView);
                    await HumanPauseAsync(1000, 1500, ct);

                    if (ct.IsCancellationRequested || !isSimulateEnabled()) break;

                    bool hasMorePages = true;

                    while (hasMorePages && !ct.IsCancellationRequested && isSimulateEnabled())
                    {
                        // 寻找并访问当前主页面中第一个未访问过的链接（从首屏开始检索）
                        LinkItem targetLink = null;
                        bool reachedPageBottom = false;

                        while (targetLink == null && !reachedPageBottom && !ct.IsCancellationRequested && isSimulateEnabled())
                        {
                            string currentMainUrl = await GetCurrentUrlAsync(webView);
                            if (string.IsNullOrWhiteSpace(currentMainUrl))
                            {
                                currentMainUrl = config.Url;
                            }

                            // 1. 获取当前屏幕视口内符合正则的所有链接
                            updateStatus(LanguageManager.Instance.GetString(
                                "AutoVisit_Status_Matching", roundVisitedCount, config.MaxVisitCount));

                            var screenLinks = await GetCurrentScreenMatchingLinksAsync(webView, config.LinkMatchRegex);
                            if (ct.IsCancellationRequested || !isSimulateEnabled()) break;

                            // 2. 筛选首个尚未访问的链接
                            var excludedList = AutoVisitConfig.ParseExcludedUrls(config.ExcludedUrls);
                            targetLink = SelectFirstUnvisitedLink(screenLinks, currentMainUrl, excludedList);

                            if (targetLink != null)
                            {
                                // 找到首个未访问链接，跳出寻找循环直接进入访问
                                break;
                            }

                            // 3. 当前屏幕没有未访问链接，检查主页面是否已达底部
                            bool atBottom = await IsPageAtBottomAsync(webView);
                            if (atBottom)
                            {
                                reachedPageBottom = true;
                                break;
                            }

                            // 4. 向下平滑滚动到下一屏继续寻找
                            updateStatus(LanguageManager.Instance["AutoVisit_Status_ScrollNextScreen"]);
                            await ScrollToNextScreenAsync(webView, ct);
                            await HumanPauseAsync(800, 1500, ct);

                            atBottom = await IsPageAtBottomAsync(webView);
                            if (atBottom)
                            {
                                // 再次检查最后一屏是否有未访问链接
                                screenLinks = await GetCurrentScreenMatchingLinksAsync(webView, config.LinkMatchRegex);
                                targetLink = SelectFirstUnvisitedLink(screenLinks, currentMainUrl, excludedList);
                                reachedPageBottom = true;
                                break;
                            }
                        }

                        if (ct.IsCancellationRequested || !isSimulateEnabled()) break;

                        // 5. 若找到未访问链接，执行进入子页面与真人滚动
                        if (targetLink != null)
                        {
                            // 记录访问历史（24小时排重）
                            AutoVisitHistoryService.Instance.RecordVisit(targetLink.Href);

                            updateStatus(LanguageManager.Instance.GetString(
                                "AutoVisit_Status_VisitingChild",
                                roundVisitedCount + 1,
                                config.MaxVisitCount,
                                string.IsNullOrWhiteSpace(targetLink.Title) ? targetLink.Href : targetLink.Title));

                            string currentMainUrl = await GetCurrentUrlAsync(webView);
                            if (string.IsNullOrWhiteSpace(currentMainUrl)) currentMainUrl = config.Url;

                            // 点击或导航进入子页面
                            bool navigated = await NavigateToChildLinkAsync(webView, targetLink.Href, ct);
                            if (navigated && !ct.IsCancellationRequested && isSimulateEnabled())
                            {
                                // 在子页面中模拟真人平滑向下滚动至页面底部
                                updateStatus(LanguageManager.Instance["AutoVisit_Status_ScrollingChild"]);
                                await ScrollChildPageToBottomAsync(webView, settings, updateStatus, ct, isSimulateEnabled);

                                // 到达底部稍作停留 (1 秒左右)
                                await HumanPauseAsync(800, 1500, ct);
                            }

                            // 6. 返回主页面并重置滚动条到首屏顶部
                            updateStatus(LanguageManager.Instance["AutoVisit_Status_ReturnMain"]);
                            await ReturnToMainPageAsync(webView, currentMainUrl, ct);

                            roundVisitedCount++;

                            if (roundVisitedCount >= config.MaxVisitCount)
                            {
                                break;
                            }

                            // 7. 访问完成返回主页后，在访问下一个链接前随机延迟指定秒数
                            int minDelay = Math.Max(0, config.MinLinkDelaySeconds);
                            int maxDelay = Math.Max(minDelay, config.MaxLinkDelaySeconds);
                            int delaySeconds = _random.Next(minDelay, maxDelay + 1);

                            for (int sec = delaySeconds; sec > 0 && !ct.IsCancellationRequested && isSimulateEnabled(); sec--)
                            {
                                updateStatus(LanguageManager.Instance.GetString("AutoVisit_Status_WaitingNextLink", sec));
                                await Task.Delay(1000, ct);
                            }

                            if (ct.IsCancellationRequested || !isSimulateEnabled())
                            {
                                break;
                            }

                            // 继续下一轮：主页面已在首屏顶部，下一次寻找将从首屏开始获取第一个未访问链接
                            continue;
                        }

                        // 8. 若当前页已达底部且无任何未访问链接，根据下一页定位判断翻页
                        if (reachedPageBottom)
                        {
                            if (!string.IsNullOrWhiteSpace(config.NextPageSelector))
                            {
                                updateStatus(LanguageManager.Instance["AutoVisit_Status_NextPage"]);
                                bool clicked = await ClickNextPageAsync(webView, config.NextPageSelector, ct);
                                if (clicked)
                                {
                                    await HumanPauseAsync(2000, 3000, ct);
                                    await ScrollToTopAsync(webView);
                                    await HumanPauseAsync(1000, 1500, ct);
                                    hasMorePages = true;
                                }
                                else
                                {
                                    hasMorePages = false;
                                }
                            }
                            else
                            {
                                hasMorePages = false;
                            }
                        }
                    }

                    if (ct.IsCancellationRequested || !isSimulateEnabled()) break;

                    // 9. 达到访问链接数或所有页面已访问完毕：在设置的刷新间隔随机分钟后，刷新页面重新从头访问
                    int minMins = Math.Max(1, config.MinRefreshIntervalMinutes);
                    int maxMins = Math.Max(minMins, config.MaxRefreshIntervalMinutes);
                    int waitMinutes = _random.Next(minMins, maxMins + 1);

                    DateTime restartTime = DateTime.Now.AddMinutes(waitMinutes);
                    updateStatus(LanguageManager.Instance.GetString(
                        "AutoVisit_Status_WaitingRefresh",
                        roundVisitedCount,
                        waitMinutes,
                        restartTime.ToString("HH:mm:ss")));

                    int waitSeconds = waitMinutes * 60;
                    while (waitSeconds > 0 && !ct.IsCancellationRequested && isSimulateEnabled())
                    {
                        int step = Math.Min(5, waitSeconds);
                        await Task.Delay(step * 1000, ct);
                        waitSeconds -= step;

                        int remMin = waitSeconds / 60;
                        int remSec = waitSeconds % 60;
                        updateStatus(LanguageManager.Instance.GetString("AutoVisit_Status_WaitingCountdown", remMin, remSec));
                    }

                    if (ct.IsCancellationRequested || !isSimulateEnabled()) break;

                    // 重新从头开始访问
                    await EnsureOnPageAsync(webView, config.Url, updateStatus, ct);
                    await ScrollToTopAsync(webView);
                    await HumanPauseAsync(1500, 2500, ct);
                }
            }
            catch (TaskCanceledException)
            {
                // Normal cancellation
            }
            catch (Exception ex)
            {
                Logger.LogError("AutoVisitService encountered an error", ex);
            }
            finally
            {
                IsRunning = false;
                if (isSimulateEnabled())
                {
                    updateStatus(LanguageManager.Instance["AutoVisit_Status_Stopped"]);
                }
            }
        }

        #region WebView2 Operations & JS Injections

        /// <summary>
        /// 获取当前屏幕视口内的所有符合正则表达式的可见链接
        /// </summary>
        private async Task<List<LinkItem>> GetCurrentScreenMatchingLinksAsync(WebView2 webView, string regexPattern)
        {
            var results = new List<LinkItem>();
            if (webView?.CoreWebView2 == null) return results;

            try
            {
                string safePattern = (regexPattern ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "");
                string script = $@"
(function() {{
    try {{
        var pattern = ""{safePattern}"";
        var reg = new RegExp(pattern, 'i');
        var links = Array.from(document.querySelectorAll('a[href]'));
        var vh = window.innerHeight || document.documentElement.clientHeight || 800;
        var vw = window.innerWidth || document.documentElement.clientWidth || 1000;
        var list = [];
        var seen = {{}};

        var curBase = window.location.href.split('#')[0].replace(/\/+$/, '').toLowerCase();
        for (var i = 0; i < links.length; i++) {{
            var a = links[i];
            var href = a.href;
            if (!href || href.startsWith('javascript:') || href.startsWith('#') || href.startsWith('mailto:')) continue;

            // 过滤当前页面自身链接或普通锚点
            var linkBase = href.split('#')[0].replace(/\/+$/, '').toLowerCase();
            if (curBase === linkBase) {{
                var curHash = window.location.hash || '';
                var linkHash = a.hash || '';
                if (!linkHash.startsWith('#/') && !linkHash.startsWith('#!/')) {{
                    continue;
                }}
                if (curHash.toLowerCase() === linkHash.toLowerCase()) {{
                    continue;
                }}
            }}

            if (!reg.test(href)) continue;
            if (seen[href]) continue;

            var rect = a.getBoundingClientRect();
            if (rect.width <= 0 || rect.height <= 0) continue;

            // 视口可见性检测
            var inViewport = rect.top < vh && rect.bottom > 0 && rect.left < vw && rect.right > 0;
            if (!inViewport) continue;

            var style = window.getComputedStyle(a);
            if (style.visibility === 'hidden' || style.display === 'none' || style.opacity === '0') continue;

            seen[href] = true;
            var text = (a.innerText || a.textContent || '').trim().replace(/[\r\n\t]+/g, ' ').substring(0, 80);
            list.push({{ href: href, title: text }});
        }}
        return JSON.stringify(list);
    }} catch (e) {{
        return JSON.stringify([]);
    }}
}})();";

                string json = await webView.CoreWebView2.ExecuteScriptAsync(script);
                if (!string.IsNullOrWhiteSpace(json) && json != "null")
                {
                    // ExecuteScriptAsync returns a JSON-encoded string
                    string unescaped = json;
                    if (unescaped.StartsWith("\"") && unescaped.EndsWith("\""))
                    {
                        unescaped = System.Text.RegularExpressions.Regex.Unescape(unescaped.Substring(1, unescaped.Length - 2));
                    }

                    var arr = JArray.Parse(unescaped);
                    foreach (var item in arr)
                    {
                        string href = item["href"]?.ToString();
                        string title = item["title"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(href))
                        {
                            results.Add(new LinkItem { Href = href, Title = title });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error getting matching links from current screen", ex);
            }

            return results;
        }

        /// <summary>
        /// 点击或导航进入子链接
        /// </summary>
        private async Task<bool> NavigateToChildLinkAsync(WebView2 webView, string href, CancellationToken ct)
        {
            if (webView?.CoreWebView2 == null) return false;

            var tcs = new TaskCompletionSource<bool>();
            EventHandler<CoreWebView2NavigationCompletedEventArgs> navHandler = null;
            navHandler = (s, e) =>
            {
                tcs.TrySetResult(e.IsSuccess);
            };

            webView.CoreWebView2.NavigationCompleted += navHandler;

            try
            {
                // 优先在页面内触发点击，并清除 target="_blank"
                string safeHref = href.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("'", "\\'");
                string clickScript = $@"
(function() {{
    var links = document.querySelectorAll('a[href]');
    for (var i = 0; i < links.length; i++) {{
        if (links[i].href === '{safeHref}') {{
            links[i].removeAttribute('target');
            links[i].scrollIntoView({{ behavior: 'smooth', block: 'center' }});
            links[i].click();
            return true;
        }}
    }}
    return false;
}})();";

                string clickResult = await webView.CoreWebView2.ExecuteScriptAsync(clickScript);
                bool clicked = clickResult == "true";

                // 如果点击未成功或未触发导航，直接 Navigate
                var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(clicked ? 3000 : 500, ct));
                if (completedTask != tcs.Task)
                {
                    // 补发导航
                    webView.CoreWebView2.Navigate(href);
                    var navTask = await Task.WhenAny(tcs.Task, Task.Delay(20000, ct));
                    return navTask == tcs.Task && await tcs.Task;
                }

                return await tcs.Task;
            }
            catch
            {
                return false;
            }
            finally
            {
                webView.CoreWebView2.NavigationCompleted -= navHandler;
            }
        }

        /// <summary>
        /// 在子页面中按模拟真人参数平滑滚动至底部
        /// </summary>
        private async Task ScrollChildPageToBottomAsync(
            WebView2 webView,
            AppSettings settings,
            Action<string> updateStatus,
            CancellationToken ct,
            Func<bool> isSimulateEnabled)
        {
            if (webView?.CoreWebView2 == null) return;

            int unchangedCount = 0;
            double lastScrollY = -1;
            int scrollCount = 0;
            const int maxScrolls = 80; // 避免无线滚动页面死循环

            while (!ct.IsCancellationRequested && isSimulateEnabled() && scrollCount < maxScrolls)
            {
                double minSec = Math.Max(0.1, settings.SimulateVisitMinIntervalSeconds);
                double maxSec = Math.Max(minSec, settings.SimulateVisitMaxIntervalSeconds);
                double intervalSec = minSec + _random.NextDouble() * (maxSec - minSec);
                int delayMs = (int)(intervalSec * 1000);

                await Task.Delay(delayMs, ct);
                if (ct.IsCancellationRequested || !isSimulateEnabled()) break;

                int minPx = Math.Max(1, settings.SimulateVisitMinScrollDistance);
                int maxPx = Math.Max(minPx, settings.SimulateVisitMaxScrollDistance);
                int distance = _random.Next(minPx, maxPx + 1);

                string scrollScript = $@"
(function() {{
    window.scrollBy({{ top: {distance}, behavior: 'smooth' }});
    var scrollHeight = Math.max(document.body.scrollHeight, document.documentElement.scrollHeight, 0);
    var clientHeight = window.innerHeight || document.documentElement.clientHeight || 800;
    var scrollY = window.scrollY || window.pageYOffset || 0;
    var atBottom = (clientHeight + scrollY) >= (scrollHeight - 25);
    return JSON.stringify({{ atBottom: atBottom, scrollY: scrollY, scrollHeight: scrollHeight }});
}})();";

                string resJson = await webView.CoreWebView2.ExecuteScriptAsync(scrollScript);
                scrollCount++;

                if (!string.IsNullOrWhiteSpace(resJson) && resJson != "null")
                {
                    try
                    {
                        string unescaped = resJson;
                        if (unescaped.StartsWith("\"") && unescaped.EndsWith("\""))
                        {
                            unescaped = System.Text.RegularExpressions.Regex.Unescape(unescaped.Substring(1, unescaped.Length - 2));
                        }

                        var obj = JObject.Parse(unescaped);
                        bool atBottom = obj["atBottom"]?.Value<bool>() ?? false;
                        double curScrollY = obj["scrollY"]?.Value<double>() ?? 0;

                        if (Math.Abs(curScrollY - lastScrollY) < 2)
                        {
                            unchangedCount++;
                        }
                        else
                        {
                            unchangedCount = 0;
                        }
                        lastScrollY = curScrollY;

                        if (atBottom || unchangedCount >= 3)
                        {
                            // 已到达子页面底部
                            break;
                        }
                    }
                    catch { }
                }
            }
        }

        /// <summary>
        /// 判断两个 URL 是否属于同一个页面（忽略普通定位锚点、大小写与尾部斜杠差异，保留单页路由）
        /// </summary>
        public bool IsSamePage(string urlA, string urlB)
        {
            if (string.IsNullOrWhiteSpace(urlA) || string.IsNullOrWhiteSpace(urlB)) return false;

            string CleanUrl(string u)
            {
                u = u.Trim();
                int hashIdx = u.IndexOf('#');
                if (hashIdx >= 0)
                {
                    string fragment = u.Substring(hashIdx);
                    // 若不是前端单页路由（如 #/ 或 #!/ ），则剔除页面内定位锚点
                    if (!fragment.StartsWith("#/") && !fragment.StartsWith("#!/"))
                    {
                        u = u.Substring(0, hashIdx);
                    }
                }
                if (Uri.TryCreate(u, UriKind.Absolute, out Uri uri))
                {
                    string path = uri.GetLeftPart(UriPartial.Path);
                    if (path.EndsWith("/") && uri.AbsolutePath.Length > 1)
                    {
                        path = path.TrimEnd('/');
                    }
                    return (path + uri.Query + uri.Fragment).ToLowerInvariant();
                }
                return u.TrimEnd('/').ToLowerInvariant();
            }

            return string.Equals(CleanUrl(urlA), CleanUrl(urlB), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 判断指定候选链接是否匹配排除列表中的任意一项（支持完全匹配、规范化匹配、目录前缀、通配符及相对路径）
        /// </summary>
        public bool IsExcludedUrl(string candidateUrl, IEnumerable<string> excludedList)
        {
            if (string.IsNullOrWhiteSpace(candidateUrl) || excludedList == null) return false;

            string normCandidate = AutoVisitHistoryService.NormalizeUrl(candidateUrl);
            Uri candUri = null;
            try
            {
                Uri.TryCreate(candidateUrl, UriKind.Absolute, out candUri);
            }
            catch { }

            foreach (var rawRule in excludedList)
            {
                if (string.IsNullOrWhiteSpace(rawRule)) continue;
                string rule = rawRule.Trim();

                // 1. 完全相同或 IsSamePage 匹配（处理锚点、尾部斜杠、大小写等）
                if (string.Equals(candidateUrl, rule, StringComparison.OrdinalIgnoreCase)) return true;
                if (IsSamePage(candidateUrl, rule)) return true;

                // 2. 规范化后比对
                string normRule = AutoVisitHistoryService.NormalizeUrl(rule);
                if (!string.IsNullOrEmpty(normRule) && string.Equals(normCandidate, normRule, StringComparison.OrdinalIgnoreCase)) return true;

                // 3. 通配符匹配 (如包含 * 或 ?)
                if (rule.Contains("*") || rule.Contains("?"))
                {
                    try
                    {
                        string pattern = "^" + Regex.Escape(rule).Replace(@"\*", ".*").Replace(@"\?", ".") + "$";
                        if (Regex.IsMatch(candidateUrl, pattern, RegexOptions.IgnoreCase)) return true;
                    }
                    catch { }
                }

                // 4. 目录前缀匹配 (如配置 https://example.com/ad/ 或 https://example.com/ad，匹配 https://example.com/ad/xxx)
                string prefix = rule.TrimEnd('/') + "/";
                if (candidateUrl.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;

                // 5. 相对路径匹配（如配置 /logout 或 /ads/*）
                if (rule.StartsWith("/") && candUri != null)
                {
                    string absPath = candUri.AbsolutePath;
                    if (string.Equals(absPath, rule, StringComparison.OrdinalIgnoreCase)) return true;
                    if (absPath.StartsWith(rule.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase)) return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 便捷重载：接收原始排除链接配置文本并判断是否匹配
        /// </summary>
        public bool IsExcludedUrl(string candidateUrl, string excludedUrlsRaw)
        {
            if (string.IsNullOrWhiteSpace(candidateUrl) || string.IsNullOrWhiteSpace(excludedUrlsRaw)) return false;
            var list = AutoVisitConfig.ParseExcludedUrls(excludedUrlsRaw);
            return IsExcludedUrl(candidateUrl, list);
        }

        /// <summary>
        /// 返回主页面并重置滚动位置到顶部首屏
        /// </summary>
        private async Task ReturnToMainPageAsync(WebView2 webView, string mainUrl, CancellationToken ct)
        {
            if (webView?.CoreWebView2 == null) return;

            string curUrl = await GetCurrentUrlAsync(webView);
            bool returnedToMain = false;

            // 1. 如果当前页面已经是主页面（例如同页面或未跳走）
            if (IsSamePage(curUrl, mainUrl))
            {
                returnedToMain = true;
            }
            else if (webView.CoreWebView2.CanGoBack)
            {
                // 2. 尝试 GoBack() 返回主页面（最多尝试 3 次，防止多层中间跳转或 SPA 历史栈）
                const int maxBackAttempts = 3;
                for (int attempt = 0; attempt < maxBackAttempts && webView.CoreWebView2.CanGoBack; attempt++)
                {
                    var tcs = new TaskCompletionSource<bool>();
                    EventHandler<CoreWebView2NavigationCompletedEventArgs> navHandler = (s, e) => tcs.TrySetResult(e.IsSuccess);
                    webView.CoreWebView2.NavigationCompleted += navHandler;

                    try
                    {
                        webView.CoreWebView2.GoBack();
                        await Task.WhenAny(tcs.Task, Task.Delay(2500, ct));
                    }
                    finally
                    {
                        webView.CoreWebView2.NavigationCompleted -= navHandler;
                    }

                    await Task.Delay(400, ct);
                    curUrl = await GetCurrentUrlAsync(webView);
                    if (IsSamePage(curUrl, mainUrl))
                    {
                        returnedToMain = true;
                        break;
                    }
                }
            }

            // 3. 如果 GoBack() 无法回到主页面（例如跨域重定向、死循环、历史栈过深等），直接强制 Navigate 回主页面
            if (!returnedToMain)
            {
                curUrl = await GetCurrentUrlAsync(webView);
                if (!IsSamePage(curUrl, mainUrl))
                {
                    var tcs = new TaskCompletionSource<bool>();
                    EventHandler<CoreWebView2NavigationCompletedEventArgs> navHandler = (s, e) => tcs.TrySetResult(e.IsSuccess);
                    webView.CoreWebView2.NavigationCompleted += navHandler;

                    try
                    {
                        webView.CoreWebView2.Navigate(mainUrl);
                        await Task.WhenAny(tcs.Task, Task.Delay(15000, ct));
                    }
                    finally
                    {
                        webView.CoreWebView2.NavigationCompleted -= navHandler;
                    }
                }
            }

            // 4. 回到主页面后，禁用浏览器历史滚动自动恢复并立即重置滚动条到顶部首屏
            await Task.Delay(400, ct);
            curUrl = await GetCurrentUrlAsync(webView);
            if (IsSamePage(curUrl, mainUrl))
            {
                try
                {
                    string resetScrollScript = @"
(function() {
    try {
        if ('scrollRestoration' in history) {
            history.scrollRestoration = 'manual';
        }
    } catch(e) {}
    window.scrollTo({ top: 0, behavior: 'instant' });
})();";
                    await webView.CoreWebView2.ExecuteScriptAsync(resetScrollScript);
                }
                catch { }
            }
        }

        /// <summary>
        /// 滚动滚动条到下一屏
        /// </summary>
        private async Task ScrollToNextScreenAsync(WebView2 webView, CancellationToken ct)
        {
            if (webView?.CoreWebView2 == null) return;
            try
            {
                string script = @"
(function() {
    var vh = window.innerHeight || document.documentElement.clientHeight || 800;
    var step = Math.floor(vh * 0.85);
    window.scrollBy({ top: step, behavior: 'smooth' });
})();";
                await webView.CoreWebView2.ExecuteScriptAsync(script);
            }
            catch (Exception ex)
            {
                Logger.LogError("Error scrolling to next screen", ex);
            }
        }

        /// <summary>
        /// 判断当前页面是否已滚动到底部
        /// </summary>
        private async Task<bool> IsPageAtBottomAsync(WebView2 webView)
        {
            if (webView?.CoreWebView2 == null) return false;
            try
            {
                string script = @"
(function() {
    var scrollHeight = Math.max(document.body.scrollHeight, document.documentElement.scrollHeight, 0);
    var clientHeight = window.innerHeight || document.documentElement.clientHeight || 800;
    var scrollY = window.scrollY || window.pageYOffset || 0;
    return (clientHeight + scrollY) >= (scrollHeight - 25);
})();";
                string res = await webView.CoreWebView2.ExecuteScriptAsync(script);
                return res == "true";
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 点击下一页元素
        /// </summary>
        private async Task<bool> ClickNextPageAsync(WebView2 webView, string selector, CancellationToken ct)
        {
            if (webView?.CoreWebView2 == null || string.IsNullOrWhiteSpace(selector)) return false;

            try
            {
                string safeSelector = selector.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("'", "\\'");
                string script = $@"
(function() {{
    try {{
        var el = document.querySelector('{safeSelector}');
        if (el) {{
            el.scrollIntoView({{ behavior: 'smooth', block: 'center' }});
            el.click();
            return true;
        }}
    }} catch (e) {{}}
    return false;
}})();";

                string res = await webView.CoreWebView2.ExecuteScriptAsync(script);
                return res == "true";
            }
            catch (Exception ex)
            {
                Logger.LogError("Error clicking next page element", ex);
                return false;
            }
        }

        /// <summary>
        /// 滚动到页面顶部
        /// </summary>
        private async Task ScrollToTopAsync(WebView2 webView)
        {
            if (webView?.CoreWebView2 == null) return;
            try
            {
                await webView.CoreWebView2.ExecuteScriptAsync("window.scrollTo({ top: 0, behavior: 'instant' });");
            }
            catch { }
        }

        /// <summary>
        /// 获取当前页面的垂直滚动坐标
        /// </summary>
        private async Task<double> GetScrollYAsync(WebView2 webView)
        {
            if (webView?.CoreWebView2 == null) return 0;
            try
            {
                string res = await webView.CoreWebView2.ExecuteScriptAsync("window.scrollY || window.pageYOffset || 0;");
                if (double.TryParse(res, out double y))
                {
                    return y;
                }
            }
            catch { }
            return 0;
        }

        /// <summary>
        /// 获取当前页面真实 URL
        /// </summary>
        private async Task<string> GetCurrentUrlAsync(WebView2 webView)
        {
            if (webView?.CoreWebView2 == null) return string.Empty;
            try
            {
                string res = await webView.CoreWebView2.ExecuteScriptAsync("window.location.href;");
                if (!string.IsNullOrWhiteSpace(res) && res != "null")
                {
                    return res.Trim('"', '\'', ' ');
                }
            }
            catch { }
            return webView.Source?.ToString() ?? string.Empty;
        }

        /// <summary>
        /// 确保当前处于目标 URL 页面
        /// </summary>
        private async Task EnsureOnPageAsync(WebView2 webView, string targetUrl, Action<string> updateStatus, CancellationToken ct)
        {
            if (webView?.CoreWebView2 == null) return;
            string curUrl = await GetCurrentUrlAsync(webView);

            if (string.IsNullOrWhiteSpace(curUrl) || !curUrl.StartsWith(targetUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
            {
                var tcs = new TaskCompletionSource<bool>();
                EventHandler<CoreWebView2NavigationCompletedEventArgs> navHandler = null;
                navHandler = (s, e) => tcs.TrySetResult(e.IsSuccess);
                webView.CoreWebView2.NavigationCompleted += navHandler;

                try
                {
                    webView.CoreWebView2.Navigate(targetUrl);
                    await Task.WhenAny(tcs.Task, Task.Delay(15000, ct));
                }
                finally
                {
                    webView.CoreWebView2.NavigationCompleted -= navHandler;
                }

                await HumanPauseAsync(1500, 2500, ct);
            }
        }

        private async Task HumanPauseAsync(int minMs, int maxMs, CancellationToken ct)
        {
            int delay = _random.Next(minMs, maxMs + 1);
            await Task.Delay(delay, ct);
        }

        #endregion
    }
}
