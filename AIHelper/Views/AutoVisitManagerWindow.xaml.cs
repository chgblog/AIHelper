// Copyright (C) 2026 chgblog
// SPDX-License-Identifier: GPL-3.0
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using AIHelper.Models;
using AIHelper.Services;

namespace AIHelper.Views
{
    public partial class AutoVisitManagerWindow : Window
    {
        private readonly ObservableCollection<AutoVisitConfig> _configs;

        public List<AutoVisitConfig> Configs { get; private set; }

        public AutoVisitManagerWindow(IEnumerable<AutoVisitConfig> existingConfigs)
        {
            InitializeComponent();

            var list = existingConfigs?.Select(c => c.Clone()).ToList() ?? new List<AutoVisitConfig>();
            _configs = new ObservableCollection<AutoVisitConfig>(list);
            dgAutoVisits.ItemsSource = _configs;
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            var newConfig = new AutoVisitConfig();
            var editWin = new AutoVisitEditWindow(newConfig, isNew: true);
            editWin.Owner = this;
            if (editWin.ShowDialog() == true)
            {
                _configs.Add(newConfig);
                dgAutoVisits.SelectedItem = newConfig;
            }
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            EditSelected();
        }

        private void DgAutoVisits_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dgAutoVisits.SelectedItem is AutoVisitConfig)
            {
                EditSelected();
            }
        }

        private void EditSelected()
        {
            if (!(dgAutoVisits.SelectedItem is AutoVisitConfig selected))
            {
                MessageBox.Show(
                    LanguageManager.Instance["AutoVisit_Manager_SelectEditWarn"],
                    LanguageManager.Instance["Notice"],
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var clone = selected.Clone();
            var editWin = new AutoVisitEditWindow(clone, isNew: false);
            editWin.Owner = this;
            if (editWin.ShowDialog() == true)
            {
                int index = _configs.IndexOf(selected);
                if (index >= 0)
                {
                    _configs[index] = clone;
                    dgAutoVisits.SelectedItem = clone;
                }
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (!(dgAutoVisits.SelectedItem is AutoVisitConfig selected))
            {
                MessageBox.Show(
                    LanguageManager.Instance["AutoVisit_Manager_SelectDeleteWarn"],
                    LanguageManager.Instance["Notice"],
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var res = MessageBox.Show(
                LanguageManager.Instance["AutoVisit_Manager_DeleteConfirm"],
                LanguageManager.Instance["Notice"],
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (res == MessageBoxResult.Yes)
            {
                _configs.Remove(selected);
            }
        }

        private void BtnClearHistory_Click(object sender, RoutedEventArgs e)
        {
            int activeCount = AutoVisitHistoryService.Instance.GetActiveCount();
            var res = MessageBox.Show(
                $"{LanguageManager.Instance["AutoVisit_Manager_ClearHistoryConfirm"]} (当前记录: {activeCount})",
                LanguageManager.Instance["Notice"],
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (res == MessageBoxResult.Yes)
            {
                AutoVisitHistoryService.Instance.ClearHistory();
                MessageBox.Show(
                    LanguageManager.Instance["AutoVisit_Manager_ClearHistoryDone"],
                    LanguageManager.Instance["Notice"],
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            Configs = _configs.ToList();
            this.DialogResult = true;
            this.Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
