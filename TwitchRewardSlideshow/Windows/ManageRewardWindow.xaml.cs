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
using TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomReward;
using TwitchRewardSlideshow.Configuration;
using WebSocketSharp;

namespace TwitchRewardSlideshow.Windows {
    public partial class ManageRewardWindow : Window {
        public ObservableCollection<RewardInfo> rewards;
        private ICollectionView _itemList;
        private RewardInfo _selectedRewardInfo;

        private bool _changing;

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
            rewards.Add(new RewardInfo("Id1", "Test Poster", 100, 30, false, RewardInfo.added));
            rewards.Add(new RewardInfo("Id2", "Titulo2", 100, 300000 / 1000.0, false, RewardInfo.added));
            rewards.Add(new RewardInfo("Id3", "Titulo3", 100, 300000 / 1000.0, true, RewardInfo.added));
            rewards.Add(new RewardInfo("Id4", "Titulo4", 1000000, 0, false, RewardInfo.notAdded));
            rewards.Add(new RewardInfo("Id5", "Titulo5", 500, 0, false, RewardInfo.notAdded));
            rewards.Add(new RewardInfo("Id6", "Titulo6", 5000, 3600000, true, RewardInfo.added));

            TwitchConfig twitchConfig = App.config.Get<TwitchConfig>();
            foreach (RewardInfo reward in rewards) {
                RewardConfig config = new(reward);
                if (twitchConfig.rewards.Exists(x => x.id == config.id)) continue;
                twitchConfig.rewards.Add(config);
            }
            App.config.Set(twitchConfig);
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
            _itemList = itemSourceList.View;

            _itemList.SortDescriptions.Clear();
            _itemList.SortDescriptions.Add(new SortDescription("time", ListSortDirection.Ascending));
            RewardsDataGrid.ItemsSource = _itemList;
        }
        #endregion

        #region UI
        private void ChangeTitleText(object sender, TextChangedEventArgs e) {
            if (_changing) return;
            if (RewardsDataGrid.SelectedItem != null) RewardsDataGrid.UnselectAllCells();
            if (PointTextBox.Text.IsNullOrEmpty()) PointTextBox.Text = "1";
            if (TimeTextBox.Text.IsNullOrEmpty())
                TimeTextBox.Text =
                    (App.config.Get<AppConfig>().obsInfo.slideTimeInMilliseconds / 1000.0 * 2 + 1)
                   .ToString(CultureInfo.InvariantCulture);
            _selectedRewardInfo = new RewardInfo(null, TitleTextBox.Text, int.Parse(PointTextBox.Text),
                                                 int.Parse(TimeTextBox.Text), ExclusiveCheckBox.IsChecked ?? false,
                                                 RewardInfo.notAdded);
            SetRewardToUI(_selectedRewardInfo);
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e) {
            Regex regex = new("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void OnDataGridSelected(object sender, RoutedEventArgs e) {
            RewardInfo info = (RewardInfo)((DataGridRow)sender).Item;
            if (info == null) return;
            _selectedRewardInfo = info;
            SetRewardToUI(_selectedRewardInfo);
        }

        private void SetRewardToUI(RewardInfo info) {
            _changing = true;
            TitleTextBox.Text = info.title;
            PointTextBox.Text = info.points.ToString(CultureInfo.InvariantCulture);
            TimeTextBox.Text = info.time.ToString(CultureInfo.InvariantCulture);
            ExclusiveCheckBox.IsChecked = info.exclusive;

            bool exist = rewards.Any(x => x.title.Equals(info.title) && x.state.Equals(RewardInfo.added));
            PointTextBox.IsEnabled = !exist;
            TimeTextBox.IsEnabled = !exist;
            ExclusiveCheckBox.IsEnabled = !exist;
            AddButton.IsEnabled = !exist;
            _changing = false;
        }

        private void ClearTextBox() {
            _changing = true;
            TitleTextBox.Text = string.Empty;
            PointTextBox.Text = string.Empty;
            TimeTextBox.Text = string.Empty;
            ExclusiveCheckBox.IsChecked = false;
            RewardsDataGrid.SelectedItem = null;
            _selectedRewardInfo = null;
            _changing = false;
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
            if (_selectedRewardInfo == null) return;
            UpdateSelectedRewardInfoValues();
            if (!CheckValues()) return;
            TwitchConfig config = App.config.Get<TwitchConfig>();
            CustomReward[] responseRewards = string.IsNullOrWhiteSpace(_selectedRewardInfo.id) ?
                                                 MakeCreateRewardRequest(config).Result.Data :
                                                 MakeUpdateRewardRequest(config).Result.Data;
            if (CheckCreateUpdateResponse(responseRewards)) return;
            CustomReward responseReward = responseRewards.First();
            SaveReward(config, responseReward);
            ClearTextBox();
        }

        private Task<UpdateCustomRewardResponse> MakeUpdateRewardRequest(TwitchConfig config) {
            AppConfig appConfig = App.config.Get<AppConfig>();
            UpdateCustomRewardRequest request = new() {
                IsEnabled = true,
                Title = _selectedRewardInfo.title,
                Cost = _selectedRewardInfo.points,
                BackgroundColor = "#3489ff",
                Prompt = FormatDescription(appConfig),
                IsUserInputRequired = true
            };

            //Send request to Twitch
            Task<UpdateCustomRewardResponse> response =
                App.twitch.helix.ChannelPoints.UpdateCustomRewardAsync(config.channelId, _selectedRewardInfo.id,
                                                                       request);
            return response;
        }

        private Task<CreateCustomRewardsResponse> MakeCreateRewardRequest(TwitchConfig config) {
            AppConfig appConfig = App.config.Get<AppConfig>();
            CreateCustomRewardsRequest request = new() {
                IsEnabled = true,
                Title = _selectedRewardInfo.title,
                Cost = _selectedRewardInfo.points,
                BackgroundColor = "#3489ff",
                Prompt = FormatDescription(appConfig),
                IsUserInputRequired = true
            };

            //Send request to Twitch
            Task<CreateCustomRewardsResponse> response =
                App.twitch.helix.ChannelPoints.CreateCustomRewardsAsync(config.channelId, request);
            return response;
        }

        private string FormatDescription(AppConfig appConfig) {
            string description = appConfig.messages.rewardMsg.Replace("%aspect_ratio%",
                                                                      appConfig.obsInfo.aspectRatio.ToString());
            double seconds = _selectedRewardInfo.time % 60;
            double minutes = Math.Truncate(_selectedRewardInfo.time / 60);
            double hours = Math.Truncate(minutes / 60);
            description = hours switch {
                <= 0 when minutes <= 0 => description.Replace("%time%",
                                                              seconds.ToString(CultureInfo.InvariantCulture) + "s"),
                <= 0 => description.Replace("%time%",
                                            minutes.ToString(CultureInfo.InvariantCulture) + "m " +
                                            seconds.ToString(CultureInfo.InvariantCulture) + "s"),
                _ => description.Replace("%time%",
                                         hours.ToString(CultureInfo.InvariantCulture) + "h" +
                                         minutes.ToString(CultureInfo.InvariantCulture) + "m" +
                                         seconds.ToString(CultureInfo.InvariantCulture) + "s")
            };
            return description;
        }

        private static bool CheckCreateUpdateResponse(CustomReward[] responseRewards) {
            if (responseRewards.Length == 0) {
                MessageBox.Show("Error, no se ha podido añadir/modificar la recomensa de twitch");
                return true;
            }
            return false;
        }

        private void SaveReward(TwitchConfig config, CustomReward responseReward) {
            //Save reward to Config
            config.rewards.Add(new RewardConfig(responseReward.Title, _selectedRewardInfo.time * 1000,
                                                _selectedRewardInfo.exclusive, responseReward.Id, responseReward.Cost));
            App.config.Set(config);

            //Remove if already exist
            rewards.Remove(rewards.FirstOrDefault(x => x.title.Equals(responseReward.Title,
                                                                      StringComparison.InvariantCultureIgnoreCase)));
            //Add the new Reward to the list
            rewards.Add(new RewardInfo(responseReward.Id, responseReward.Title, responseReward.Cost,
                                       _selectedRewardInfo.time, _selectedRewardInfo.exclusive, RewardInfo.added));
        }
        #endregion

        #region DeleteRewards
        private void ClickDelete(object sender, RoutedEventArgs e) {
            TwitchConfig config = App.config.Get<TwitchConfig>();
            App.twitch.helix.ChannelPoints.DeleteCustomRewardAsync(config.channelId, _selectedRewardInfo.id);
            rewards.Remove(_selectedRewardInfo);
        }
        #endregion

        #region UpdateRewardInfo
        private void UpdateSelectedRewardInfoValues() {
            _selectedRewardInfo.title = TitleTextBox.Text;
            _selectedRewardInfo.time = int.Parse(TimeTextBox.Text);
            _selectedRewardInfo.points = int.Parse(PointTextBox.Text);
            _selectedRewardInfo.exclusive = (bool)ExclusiveCheckBox.IsChecked!;
        }

        private bool CheckValues() {
            if (rewards.Any(x => x.title.Equals(_selectedRewardInfo.title) && x.state == RewardInfo.added)) {
                MessageBox.Show("Borra antes de añadir algo que ya existe");
                return false;
            }
            if (_selectedRewardInfo.title.IsNullOrEmpty()) {
                MessageBox.Show("El titulo no puede ser vacio");
                return false;
            }
            if (_selectedRewardInfo.points <= 0) {
                MessageBox.Show("Los puntos no pueden ser 0 o menos de 0");
                return false;
            }
            double time = App.config.Get<AppConfig>().obsInfo.slideTimeInMilliseconds / 1000.0 * 2 + 1;
            if (_selectedRewardInfo.time < time) {
                MessageBox.Show($"El tiempo no puede ser menos de {time}s (TiempoEntreDiapositivas * 2 + 1s)");
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
            public double time { get; set; }
            public bool exclusive { get; set; }
            public string state { get; set; }

            public RewardInfo(string id, string title, int points, double time, bool exclusive, string state) {
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