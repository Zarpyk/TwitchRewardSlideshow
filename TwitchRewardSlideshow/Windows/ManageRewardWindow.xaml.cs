using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using TwitchLib.Api.Helix.Models.ChannelPoints;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;
using TwitchLib.Api.Helix.Models.ChannelPoints.GetCustomReward;
using TwitchRewardSlideshow.Configuration;
using WebSocketSharp;

namespace TwitchRewardSlideshow.Windows {
    public partial class ManageRewardWindow : Window {
        public ObservableCollection<RewardInfo> rewards;
        private ICollectionView itemList;
        private RewardInfo selectedRewardInfo;

        private bool changing;

        public ManageRewardWindow() {
            InitializeComponent();
            rewards = new ObservableCollection<RewardInfo>();
            TwitchConfig config = App.config.Get<TwitchConfig>();
#if DEBUG
            rewards.Add(new RewardInfo("Id1", "Titulo1", 100,
                                       300000 / 1000, false, RewardInfo.added));
            rewards.Add(new RewardInfo("Id2", "Titulo2", 100,
                                       300000 / 1000, false, RewardInfo.added));
            rewards.Add(new RewardInfo("Id3", "Titulo3", 100,
                                       300000 / 1000, true, RewardInfo.added));
            rewards.Add(new RewardInfo("Id4", "Titulo4", 1000000, 0, false, RewardInfo.notAdded));
            rewards.Add(new RewardInfo("Id5", "Titulo5", 500, 0, false, RewardInfo.notAdded));
            rewards.Add(new RewardInfo("Id6", "Titulo6", 5000, 3600000, true, RewardInfo.added));
#endif
#if RELEASE
            List<CustomReward> twitchRewards = GetRewards();
            List<RewardConfig> removeReward =
                config.rewards.Where(reward => !twitchRewards.Any(x => x.Id.Equals(reward.id))).ToList();
            config.rewards = config.rewards.Except(removeReward).ToList();
            foreach (RewardInfo info in from reward in twitchRewards
                                        let rewardConfig = config.rewards.FirstOrDefault(x => x.id.Equals(reward.Id))
                                        select rewardConfig == null ?
                                                   new RewardInfo(reward.Id, reward.Title, reward.Cost, 0, false,
                                                                  RewardInfo.notAdded) :
                                                   new RewardInfo(reward.Id, reward.Title, reward.Cost,
                                                                  rewardConfig.timeInMilliseconds / 1000,
                                                                  rewardConfig.exclusiveImage, RewardInfo.added)) {
                rewards.Add(info);
            }
#endif

            CollectionViewSource itemSourceList = new() { Source = rewards };
            itemList = itemSourceList.View;

            itemList.SortDescriptions.Clear();
            itemList.SortDescriptions.Add(new SortDescription("time", ListSortDirection.Ascending));
            RewardsDataGrid.ItemsSource = itemList;
        }

        private void ChangeTitleText(object sender, TextChangedEventArgs e) {
            if (changing) return;
            if (RewardsDataGrid.SelectedItem != null) RewardsDataGrid.UnselectAllCells();
            if (PointTextBox.Text.IsNullOrEmpty()) PointTextBox.Text = "1";
            if (TimeTextBox.Text.IsNullOrEmpty()) TimeTextBox.Text = "10";
            selectedRewardInfo = new RewardInfo(null, TitleTextBox.Text, int.Parse(PointTextBox.Text),
                                                int.Parse(TimeTextBox.Text), ExclusiveCheckBox.IsChecked ?? false,
                                                RewardInfo.notAdded);
            SetRewardToUI(selectedRewardInfo);
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e) {
            Regex regex = new("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void OnDataGridSelected(object sender, RoutedEventArgs e) {
            RewardInfo info = (RewardInfo)((DataGridRow)sender).Item;
            if (info == null) return;
            selectedRewardInfo = info;
            SetRewardToUI(selectedRewardInfo);
        }

        private void SetRewardToUI(RewardInfo info) {
            changing = true;
            TitleTextBox.Text = info.title;
            PointTextBox.Text = info.points.ToString(CultureInfo.InvariantCulture);
            TimeTextBox.Text = info.time.ToString(CultureInfo.InvariantCulture);
            ExclusiveCheckBox.IsChecked = info.exclusive;

            bool exist = rewards.Any(x => x.title.Equals(info.title) && x.state.Equals(RewardInfo.added));
            PointTextBox.IsEnabled = !exist;
            TimeTextBox.IsEnabled = !exist;
            ExclusiveCheckBox.IsEnabled = !exist;
            AddButton.IsEnabled = !exist;
            changing = false;
        }

        private void ClickAdd(object sender, RoutedEventArgs e) {
            if (rewards.Any(x => x.title.Equals(selectedRewardInfo.title) && x.state == RewardInfo.added))
                MessageBox.Show("Borra antes de añadir algo que ya existe");
            if (selectedRewardInfo.title.IsNullOrEmpty()) MessageBox.Show("El titulo no puede ser vacio");
            if (selectedRewardInfo.points <= 0) MessageBox.Show("Los puntos no pueden ser 0 o menos de 0");
            if (selectedRewardInfo.time < 10) MessageBox.Show("El tiempo no puede ser menos de 10s");
            TwitchConfig config = App.config.Get<TwitchConfig>();
            CreateCustomRewardsRequest request = new() {
                Title = selectedRewardInfo.title,
                Cost = selectedRewardInfo.points,
                BackgroundColor = "#3489ff",
                IsUserInputRequired = true
            };
            Task<CreateCustomRewardsResponse> response =
                App.twitch.helix.ChannelPoints.CreateCustomRewardsAsync(config.channelId, request);
            if (response.Result.Data.Length == 0) {
                MessageBox.Show("Error, no se ha podido añadir la recomensa a twitch");
                return;
            }
            CustomReward responseReward = response.Result.Data.First();
            config.rewards.Add(new RewardConfig(responseReward.Title, selectedRewardInfo.time * 1000,
                                                selectedRewardInfo.exclusive, responseReward.Id, responseReward.Cost));
            rewards.Remove(rewards.FirstOrDefault(x => x.title.Equals(responseReward.Title,
                                                                      StringComparison.InvariantCultureIgnoreCase)));
            rewards.Add(new RewardInfo(responseReward.Id, responseReward.Title, responseReward.Cost,
                                       selectedRewardInfo.time / 1000, selectedRewardInfo.exclusive, RewardInfo.added));
        }

        private void ClickDelete(object sender, RoutedEventArgs e) {
            TwitchConfig config = App.config.Get<TwitchConfig>();
            App.twitch.helix.ChannelPoints.DeleteCustomRewardAsync(config.channelId, selectedRewardInfo.id);
            rewards.Remove(selectedRewardInfo);
        }

        private List<CustomReward> GetRewards() {
            TwitchConfig config = App.config.Get<TwitchConfig>();
            GetCustomRewardsResponse response =
                App.twitch.helix.ChannelPoints.GetCustomRewardAsync(config.channelId).Result;
            return new List<CustomReward>(response.Data);
        }

        public class RewardInfo {
            public const string added = "Añadido";
            public const string notAdded = "No añadido";

            public string id { get; set; }
            public string title { get; set; }
            public int points { get; set; }
            public int time { get; set; }
            public bool exclusive { get; set; }
            public string state { get; set; }

            public RewardInfo(string id, string title, int points, int time, bool exclusive, string state) {
                this.id = id;
                this.title = title;
                this.points = points;
                this.time = time;
                this.exclusive = exclusive;
                this.state = state;
            }
        }
    }
}