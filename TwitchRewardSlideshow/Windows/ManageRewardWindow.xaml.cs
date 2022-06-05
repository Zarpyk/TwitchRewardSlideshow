using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
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

            DebugAddRewards();
            if (ReleaseSetUpRewards()) return;

            SetupItemList();
        }

        #region Init
        private void DebugAddRewards() {
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
        }

        private bool ReleaseSetUpRewards() {
#if RELEASE
            TwitchConfig config = App.config.Get<TwitchConfig>();
            List<CustomReward> twitchRewards = GetRewards();
            if (twitchRewards == null) {
                Close();
                return true;
            }
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
            return false;
        }

        private void SetupItemList() {
            CollectionViewSource itemSourceList = new() { Source = rewards };
            itemList = itemSourceList.View;

            itemList.SortDescriptions.Clear();
            itemList.SortDescriptions.Add(new SortDescription("time", ListSortDirection.Ascending));
            RewardsDataGrid.ItemsSource = itemList;
        }
        #endregion

        #region UI
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

        private void ClearTextBox() {
            changing = true;
            TitleTextBox.Text = string.Empty;
            PointTextBox.Text = string.Empty;
            TimeTextBox.Text = string.Empty;
            ExclusiveCheckBox.IsChecked = false;
            RewardsDataGrid.SelectedItem = null;
            selectedRewardInfo = null;
            changing = false;
        }
        #endregion

        #region GetRewards
        private List<CustomReward> GetRewards() {
            TwitchConfig config = App.config.Get<TwitchConfig>();
            GetCustomRewardsResponse response = null;
            try {
                response = App.twitch.helix.ChannelPoints.GetCustomRewardAsync(config.channelId).Result;
            } catch (Exception e) {
                Console.WriteLine(e);
                MessageBox.Show("Hubo un error para conseguir la lista de recompensas de Twitch");
            }
            return response != null ? new List<CustomReward>(response.Data) : null;
        }
        #endregion

        #region AddReward
        private void ClickAdd(object sender, RoutedEventArgs e) {
            if (selectedRewardInfo == null) return;
            UpdateSelectedRewardInfoValues();
            if (!CheckValues()) return;
            TwitchConfig config = App.config.Get<TwitchConfig>();
            Task<CreateCustomRewardsResponse> response = MakeAddRewardRequest(config);
            if (CheckAddResponse(response)) return;
            CustomReward responseReward = response.Result.Data.First();
            SaveReward(config, responseReward);
            ClearTextBox();
        }

        private Task<CreateCustomRewardsResponse> MakeAddRewardRequest(TwitchConfig config) {
            AppConfig appConfig = App.config.Get<AppConfig>();
            CreateCustomRewardsRequest request = new() {
                IsEnabled = true,
                Title = selectedRewardInfo.title,
                Cost = selectedRewardInfo.points,
                BackgroundColor = "#3489ff",
                Prompt = appConfig.messages.rewardMsg.Replace("%aspect_ratio%", appConfig.obsInfo.aspectRatio),
                IsUserInputRequired = true
            };

            //Send request to Twitch
            Task<CreateCustomRewardsResponse> response =
                App.twitch.helix.ChannelPoints.CreateCustomRewardsAsync(config.channelId, request);
            return response;
        }

        private static bool CheckAddResponse(Task<CreateCustomRewardsResponse> response) {
            if (response.Result.Data.Length == 0) {
                MessageBox.Show("Error, no se ha podido añadir la recomensa a twitch");
                return true;
            }
            return false;
        }

        private void SaveReward(TwitchConfig config, CustomReward responseReward) {
            //Save reward to Config
            config.rewards.Add(new RewardConfig(responseReward.Title, selectedRewardInfo.time * 1000,
                                                selectedRewardInfo.exclusive, responseReward.Id, responseReward.Cost));
            App.config.Set(config);

            //Remove if already exist
            rewards.Remove(rewards.FirstOrDefault(x => x.title.Equals(responseReward.Title,
                                                                      StringComparison.InvariantCultureIgnoreCase)));
            //Add the new Reward to the list
            rewards.Add(new RewardInfo(responseReward.Id, responseReward.Title, responseReward.Cost,
                                       selectedRewardInfo.time, selectedRewardInfo.exclusive, RewardInfo.added));
        }
        #endregion

        #region DeleteRewards
        private void ClickDelete(object sender, RoutedEventArgs e) {
            TwitchConfig config = App.config.Get<TwitchConfig>();
            App.twitch.helix.ChannelPoints.DeleteCustomRewardAsync(config.channelId, selectedRewardInfo.id);
            rewards.Remove(selectedRewardInfo);
        }
        #endregion

        #region UpdateRewardInfo
        private void UpdateSelectedRewardInfoValues() {
            selectedRewardInfo.title = TitleTextBox.Text;
            selectedRewardInfo.time = int.Parse(TimeTextBox.Text);
            selectedRewardInfo.points = int.Parse(PointTextBox.Text);
            selectedRewardInfo.exclusive = (bool)ExclusiveCheckBox.IsChecked!;
        }

        private bool CheckValues() {
            if (rewards.Any(x => x.title.Equals(selectedRewardInfo.title) && x.state == RewardInfo.added)) {
                MessageBox.Show("Borra antes de añadir algo que ya existe");
                return false;
            }
            if (selectedRewardInfo.title.IsNullOrEmpty()) {
                MessageBox.Show("El titulo no puede ser vacio");
                return false;
            }
            if (selectedRewardInfo.points <= 0) {
                MessageBox.Show("Los puntos no pueden ser 0 o menos de 0");
                return false;
            }
            if (selectedRewardInfo.time < 10) {
                MessageBox.Show("El tiempo no puede ser menos de 10s");
                return false;
            }
            return true;
        }
        #endregion

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