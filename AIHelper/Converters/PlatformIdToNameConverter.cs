// Copyright (C) 2026 chgblog
// SPDX-License-Identifier: GPL-3.0
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using AIHelper.Models;
using AIHelper.Services;

namespace AIHelper.Converters
{
    /// <summary>
    /// Converts a PlatformId to the platform display name.
    /// Requires the platforms list to be set before use.
    /// </summary>
    public class PlatformIdToNameConverter : IValueConverter
    {
        public List<AiPlatform> Platforms { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string platformId = value as string;
            if (string.IsNullOrEmpty(platformId))
            {
                return LanguageManager.Instance["Settings_Action_Platform_Default"];
            }

            if (Platforms != null)
            {
                var platform = Platforms.FirstOrDefault(p => p.Id == platformId);
                if (platform != null)
                {
                    return platform.Name;
                }
            }

            return LanguageManager.Instance["Settings_Action_Platform_Default"];
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
