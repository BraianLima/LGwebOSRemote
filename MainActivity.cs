using Android.Content;
using Android.Graphics;
using Android.Views;
using Android.Widget;
using AndroidX.AppCompat.App;
using Google.Android.Material.BottomNavigation;
using LGwebOSRemote.Models;
using LGwebOSRemote.Services;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LGwebOSRemote;

[Activity(Label = "@string/app_name", MainLauncher = true, Theme = "@style/Theme.LGwebOSRemote", WindowSoftInputMode = SoftInput.AdjustResize)]
public class MainActivity : AppCompatActivity, WebOSClient.IListener
{
    private ISharedPreferences? _prefs;
    private WebOSClient? _client;

    private TextView? _tvTitle;
    private TextView? _tvStatus;
    private ImageView? _imgStatusDot;
    private TextView? _txtBanner;
    private TextView? _txtCurrentApp;
    private View? _cardWakeTv;
    private Button? _btnWakeTv;

    // Tabs
    private View? _tabRemote;
    private View? _tabAudio;
    private View? _tabApps;
    private View? _tabInputs;
    private View? _tabPicture;
    private View? _tabSettings;

    // Audio views
    private TextView? _txtVolumeVal;
    private TextView? _txtAudioStatus;
    private SeekBar? _seekVolume;
    private Button? _btnMute;

    // Apps views
    private EditText? _editSearchApps;
    private ListView? _listApps;
    private List<TVApp> _allApps = new();
    private List<TVApp> _displayedApps = new();
    private ArrayAdapter<string>? _appsAdapter;

    // Inputs views
    private ListView? _listInputs;
    private List<TVInput> _allInputs = new();
    private ArrayAdapter<string>? _inputsAdapter;

    // Picture views
    private TextView? _txtLblBacklight;
    private TextView? _txtLblBrightness;
    private TextView? _txtLblContrast;
    private TextView? _txtLblColor;
    private SeekBar? _seekBacklight;
    private SeekBar? _seekBrightness;
    private SeekBar? _seekContrast;
    private SeekBar? _seekColor;

    // Settings views
    private Button? _btnDiscover;
    private ListView? _listDevices;
    private EditText? _editIp;
    private EditText? _editName;
    private EditText? _editMac;
    private Button? _btnSaveConnect;
    private Button? _btnForget;
    private List<TVDevice> _discoveredDevices = new();

    private bool _isMuted;
    private int _currentVolume = 20;
    private string _currentHost = "";
    private string _currentName = "LG webOS TV";
    private string _currentModel = "webOS";
    private string _currentMac = "";

    protected override void AttachBaseContext(Context? @base)
    {
        if (@base != null)
        {
            var prefs = @base.GetSharedPreferences("lgwebos_prefs", FileCreationMode.Private);
            var lang = prefs?.GetString("app_language", "pt") ?? "pt";
            var locale = new Java.Util.Locale(lang);
            Java.Util.Locale.Default = locale;

            var config = new Android.Content.Res.Configuration(@base.Resources?.Configuration);
            config.SetLocale(locale);
            var localizedContext = @base.CreateConfigurationContext(config);
            base.AttachBaseContext(localizedContext);
            return;
        }
        base.AttachBaseContext(@base);
    }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        _prefs = GetSharedPreferences("lgwebos_prefs", FileCreationMode.Private);
        var lang = _prefs?.GetString("app_language", "pt") ?? "pt";
        var locale = new Java.Util.Locale(lang);
        Java.Util.Locale.Default = locale;
        var config = new Android.Content.Res.Configuration(Resources?.Configuration);
        config.SetLocale(locale);
        Resources?.UpdateConfiguration(config, Resources.DisplayMetrics);

        base.OnCreate(savedInstanceState);
        SetContentView(Resource.Layout.activity_main);

        _currentHost = _prefs?.GetString("tv_host", "") ?? "";
        _currentName = _prefs?.GetString("tv_name", "LG webOS TV") ?? "LG webOS TV";
        _currentModel = _prefs?.GetString("tv_model", "webOS") ?? "webOS";
        _currentMac = _prefs?.GetString("tv_mac", "") ?? "";

        InitViews();
        SetupNavigation();
        SetupRemoteTab();
        SetupAudioTab();
        SetupAppsTab();
        SetupPictureTab();
        SetupSettingsTab();

        LoadAppCatalog();

        if (!string.IsNullOrEmpty(_currentHost))
        {
            ConnectToTV();
        }
    }

    private void InitViews()
    {
        _tvTitle = FindViewById<TextView>(Resource.Id.tv_title);
        _tvStatus = FindViewById<TextView>(Resource.Id.tv_status);
        _imgStatusDot = FindViewById<ImageView>(Resource.Id.img_status_dot);
        _txtBanner = FindViewById<TextView>(Resource.Id.txt_banner);
        _txtCurrentApp = FindViewById<TextView>(Resource.Id.txt_current_app);
        _cardWakeTv = FindViewById<View>(Resource.Id.card_wake_tv);
        _btnWakeTv = FindViewById<Button>(Resource.Id.btn_wake_tv);

        _tabRemote = FindViewById<View>(Resource.Id.tab_remote);
        _tabAudio = FindViewById<View>(Resource.Id.tab_audio);
        _tabApps = FindViewById<View>(Resource.Id.tab_apps);
        _tabInputs = FindViewById<View>(Resource.Id.tab_inputs);
        _tabPicture = FindViewById<View>(Resource.Id.tab_picture);
        _tabSettings = FindViewById<View>(Resource.Id.tab_settings);

        _tvTitle!.Text = _currentName;

        _btnWakeTv!.Click += (s, e) =>
        {
            if (_client?.IsConnected == true)
            {
                _ = _client.TurnOffAsync();
                ShowBanner(GetString(Resource.String.banner_turning_off));
                UpdateConnectionStatus(false, GetString(Resource.String.status_disconnected));
            }
            else
            {
                ShowBanner(GetString(Resource.String.banner_connecting));
                ConnectToTV();
            }
        };
    }

    private void SetupNavigation()
    {
        var bottomNav = FindViewById<BottomNavigationView>(Resource.Id.bottom_nav);
        bottomNav!.ItemSelected += (s, e) =>
        {
            _tabRemote!.Visibility = ViewStates.Gone;
            _tabAudio!.Visibility = ViewStates.Gone;
            _tabApps!.Visibility = ViewStates.Gone;
            _tabInputs!.Visibility = ViewStates.Gone;
            _tabPicture!.Visibility = ViewStates.Gone;
            _tabSettings!.Visibility = ViewStates.Gone;

            int id = e.Item.ItemId;
            if (id == Resource.Id.nav_remote) _tabRemote.Visibility = ViewStates.Visible;
            else if (id == Resource.Id.nav_audio) _tabAudio.Visibility = ViewStates.Visible;
            else if (id == Resource.Id.nav_apps) _tabApps.Visibility = ViewStates.Visible;
            else if (id == Resource.Id.nav_inputs) _tabInputs.Visibility = ViewStates.Visible;
            else if (id == Resource.Id.nav_picture) _tabPicture.Visibility = ViewStates.Visible;
            else if (id == Resource.Id.nav_settings) _tabSettings.Visibility = ViewStates.Visible;
        };
    }

    private void SetupRemoteTab()
    {
        // Navigation buttons
        FindViewById<Button>(Resource.Id.btn_home)!.Click += (s, e) => _client?.SendButton("HOME");
        FindViewById<Button>(Resource.Id.btn_back)!.Click += (s, e) => _client?.SendButton("BACK");
        FindViewById<Button>(Resource.Id.btn_menu)!.Click += (s, e) => _client?.SendButton("MENU");
        FindViewById<Button>(Resource.Id.btn_exit)!.Click += (s, e) => _client?.SendButton("EXIT");

        // Quick Streaming App Shortcuts (Netflix, YouTube, HBO Max, Prime Video)
        FindViewById<Button>(Resource.Id.btn_quick_netflix)?.Click += (s, e) =>
        {
            _ = _client?.LaunchAppAsync("netflix");
            ShowBanner("Abrindo Netflix...");
        };
        FindViewById<Button>(Resource.Id.btn_quick_youtube)?.Click += (s, e) =>
        {
            _ = _client?.LaunchAppAsync("youtube.leanback.v4");
            ShowBanner("Abrindo YouTube...");
        };
        FindViewById<Button>(Resource.Id.btn_quick_hbomax)?.Click += async (s, e) =>
        {
            ShowBanner("Abrindo HBO Max...");
            try
            {
                await (_client?.LaunchAppAsync("com.wbd.stream") ?? Task.CompletedTask);
            }
            catch
            {
                _ = _client?.LaunchAppAsync("com.hbo.hbomax");
            }
        };
        FindViewById<Button>(Resource.Id.btn_quick_prime)?.Click += (s, e) =>
        {
            _ = _client?.LaunchAppAsync("amazon");
            ShowBanner("Abrindo Prime Video...");
        };

        // D-Pad directional buttons
        FindViewById<Button>(Resource.Id.btn_up)!.Click += (s, e) => _client?.SendButton("UP");
        FindViewById<Button>(Resource.Id.btn_down)!.Click += (s, e) => _client?.SendButton("DOWN");
        FindViewById<Button>(Resource.Id.btn_left)!.Click += (s, e) => _client?.SendButton("LEFT");
        FindViewById<Button>(Resource.Id.btn_right)!.Click += (s, e) => _client?.SendButton("RIGHT");

        // Center OK button - Uses SendOk (ENTER key) to activate the currently selected item on TV!
        FindViewById<Button>(Resource.Id.btn_ok)!.Click += (s, e) => _client?.SendOk();

        // Quick Volume & Channels
        FindViewById<Button>(Resource.Id.btn_quick_vol_down)!.Click += (s, e) => _ = _client?.VolumeDownAsync();
        FindViewById<Button>(Resource.Id.btn_quick_vol_up)!.Click += (s, e) => _ = _client?.VolumeUpAsync();
        FindViewById<Button>(Resource.Id.btn_quick_mute)!.Click += (s, e) => _ = _client?.SetMuteAsync(!_isMuted);
        FindViewById<Button>(Resource.Id.btn_ch_down)!.Click += (s, e) => _client?.SendButton("CHANNELDOWN");
        FindViewById<Button>(Resource.Id.btn_ch_up)!.Click += (s, e) => _client?.SendButton("CHANNELUP");

        // Touchpad (Magic Pointer)
        var touchpad = FindViewById<View>(Resource.Id.view_touchpad);
        float lastX = 0, lastY = 0;
        bool isClick = false;
        long downTime = 0;

        touchpad!.Touch += (s, e) =>
        {
            switch (e.Event?.Action)
            {
                case MotionEventActions.Down:
                    lastX = e.Event.GetX();
                    lastY = e.Event.GetY();
                    downTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    isClick = true;
                    break;

                case MotionEventActions.Move:
                    float dx = e.Event.GetX() - lastX;
                    float dy = e.Event.GetY() - lastY;
                    lastX = e.Event.GetX();
                    lastY = e.Event.GetY();
                    if (Math.Abs(dx) > 3 || Math.Abs(dy) > 3)
                    {
                        isClick = false;
                        _client?.MovePointer((int)(dx * 1.5f), (int)(dy * 1.5f));
                    }
                    break;

                case MotionEventActions.Up:
                    long duration = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - downTime;
                    if (isClick && duration < 300)
                    {
                        _client?.ClickPointer();
                    }
                    break;
            }
        };

        // Media controls
        FindViewById<Button>(Resource.Id.btn_rewind)!.Click += (s, e) => _ = _client?.MediaControlAsync("rewind");
        FindViewById<Button>(Resource.Id.btn_play)!.Click += (s, e) => _ = _client?.MediaControlAsync("play");
        FindViewById<Button>(Resource.Id.btn_pause)!.Click += (s, e) => _ = _client?.MediaControlAsync("pause");
        FindViewById<Button>(Resource.Id.btn_stop)!.Click += (s, e) => _ = _client?.MediaControlAsync("stop");
        FindViewById<Button>(Resource.Id.btn_forward)!.Click += (s, e) => _ = _client?.MediaControlAsync("fastForward");

        // Virtual keyboard text input
        var editInput = FindViewById<EditText>(Resource.Id.edit_text_input);
        FindViewById<Button>(Resource.Id.btn_send_text)!.Click += async (s, e) =>
        {
            string text = editInput?.Text?.Trim() ?? "";
            if (!string.IsNullOrEmpty(text))
            {
                if (_client == null || !_client.IsConnected)
                {
                    ShowBanner("Conecte-se à TV primeiro.");
                    return;
                }

                try
                {
                    await _client.SendTextAsync(text);
                    ShowBanner($"Texto enviado: \"{text}\"");
                    if (editInput != null) editInput.Text = "";
                }
                catch (Exception ex)
                {
                    ShowBanner($"Erro ao enviar texto: {ex.Message}. Se necessário, use 'Reparear TV' nos Ajustes.");
                }
            }
        };

        FindViewById<Button>(Resource.Id.btn_enter_key)!.Click += async (s, e) =>
        {
            if (_client != null && _client.IsConnected)
            {
                try
                {
                    await _client.SendEnterKeyAsync();
                }
                catch
                {
                    _client.SendOk();
                }
            }
        };
    }

    private void SetupAudioTab()
    {
        _txtVolumeVal = FindViewById<TextView>(Resource.Id.txt_volume_val);
        _txtAudioStatus = FindViewById<TextView>(Resource.Id.txt_audio_status);
        _seekVolume = FindViewById<SeekBar>(Resource.Id.seek_volume);
        _btnMute = FindViewById<Button>(Resource.Id.btn_mute);

        _seekVolume!.ProgressChanged += (s, e) =>
        {
            if (e.FromUser)
            {
                _currentVolume = e.Progress;
                _txtVolumeVal!.Text = $"{_currentVolume}";
                _ = _client?.SetVolumeAsync(_currentVolume);
            }
        };

        FindViewById<Button>(Resource.Id.btn_vol_down)!.Click += (s, e) => _ = _client?.VolumeDownAsync();
        FindViewById<Button>(Resource.Id.btn_vol_up)!.Click += (s, e) => _ = _client?.VolumeUpAsync();
        _btnMute!.Click += (s, e) => _ = _client?.SetMuteAsync(!_isMuted);
    }

    private void SetupAppsTab()
    {
        _editSearchApps = FindViewById<EditText>(Resource.Id.edit_search_apps);
        _listApps = FindViewById<ListView>(Resource.Id.list_apps);

        _editSearchApps!.TextChanged += (s, e) =>
        {
            string query = _editSearchApps.Text?.Trim() ?? "";
            _displayedApps = _allApps
                .Where(a => a.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
                .OrderBy(a => a.Title, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            var titles = _displayedApps.Select(a => a.Title).ToList();
            _appsAdapter = new ArrayAdapter<string>(this, Android.Resource.Layout.SimpleListItem1, titles);
            _listApps!.Adapter = _appsAdapter;
        };

        _listApps!.ItemClick += (s, e) =>
        {
            if (e.Position >= 0 && e.Position < _displayedApps.Count)
            {
                var app = _displayedApps[e.Position];
                _ = _client?.LaunchAppAsync(app.Id);
                ShowBanner($"Abrindo {app.Title}...");
            }
        };

        // Inputs tab
        _listInputs = FindViewById<ListView>(Resource.Id.list_inputs);
        _listInputs!.ItemClick += (s, e) =>
        {
            if (e.Position >= 0 && e.Position < _allInputs.Count)
            {
                var input = _allInputs[e.Position];
                _ = _client?.SwitchInputAsync(input.Id);
                ShowBanner($"Alternando para {input.Label}...");
            }
        };
    }

    private void SetupPictureTab()
    {
        _txtLblBacklight = FindViewById<TextView>(Resource.Id.txt_lbl_backlight);
        _txtLblBrightness = FindViewById<TextView>(Resource.Id.txt_lbl_brightness);
        _txtLblContrast = FindViewById<TextView>(Resource.Id.txt_lbl_contrast);
        _txtLblColor = FindViewById<TextView>(Resource.Id.txt_lbl_color);

        _seekBacklight = FindViewById<SeekBar>(Resource.Id.seek_backlight);
        _seekBrightness = FindViewById<SeekBar>(Resource.Id.seek_brightness);
        _seekContrast = FindViewById<SeekBar>(Resource.Id.seek_contrast);
        _seekColor = FindViewById<SeekBar>(Resource.Id.seek_color);

        _seekBacklight!.ProgressChanged += (s, e) => _txtLblBacklight!.Text = $"Luz de Fundo (Backlight): {e.Progress}";
        _seekBrightness!.ProgressChanged += (s, e) => _txtLblBrightness!.Text = $"Brilho: {e.Progress}";
        _seekContrast!.ProgressChanged += (s, e) => _txtLblContrast!.Text = $"Contraste: {e.Progress}";
        _seekColor!.ProgressChanged += (s, e) => _txtLblColor!.Text = $"Cor: {e.Progress}";

        FindViewById<Button>(Resource.Id.btn_apply_picture)!.Click += async (s, e) =>
        {
            var p = new PictureSettings(
                Backlight: _seekBacklight.Progress,
                Brightness: _seekBrightness.Progress,
                Contrast: _seekContrast.Progress,
                Color: _seekColor.Progress
            );
            if (_client != null && _client.IsConnected)
            {
                await _client.SetPictureSettingsAsync(p);
                ShowBanner("Ajustes de imagem aplicados na TV!");
            }
        };
    }

    private void SetupSettingsTab()
    {
        _btnDiscover = FindViewById<Button>(Resource.Id.btn_discover);
        _listDevices = FindViewById<ListView>(Resource.Id.list_devices);
        _editIp = FindViewById<EditText>(Resource.Id.edit_ip);
        _editName = FindViewById<EditText>(Resource.Id.edit_name);
        _editMac = FindViewById<EditText>(Resource.Id.edit_mac);
        _btnSaveConnect = FindViewById<Button>(Resource.Id.btn_save_connect);
        _btnForget = FindViewById<Button>(Resource.Id.btn_forget);

        var btnLangPt = FindViewById<Button>(Resource.Id.btn_lang_pt);
        var btnLangEn = FindViewById<Button>(Resource.Id.btn_lang_en);
        var btnLangEs = FindViewById<Button>(Resource.Id.btn_lang_es);

        var currentLang = _prefs?.GetString("app_language", "pt") ?? "pt";
        var primaryColor = Color.ParseColor("#A50034");
        var cardColor = Color.ParseColor("#1E1B24");

        if (btnLangPt != null) btnLangPt.SetBackgroundColor(currentLang == "pt" ? primaryColor : cardColor);
        if (btnLangEn != null) btnLangEn.SetBackgroundColor(currentLang == "en" ? primaryColor : cardColor);
        if (btnLangEs != null) btnLangEs.SetBackgroundColor(currentLang == "es" ? primaryColor : cardColor);

        if (btnLangPt != null) btnLangPt.Click += (s, e) => SetAppLanguage("pt");
        if (btnLangEn != null) btnLangEn.Click += (s, e) => SetAppLanguage("en");
        if (btnLangEs != null) btnLangEs.Click += (s, e) => SetAppLanguage("es");

        _editIp!.Text = _currentHost;
        _editName!.Text = _currentName;

        _btnDiscover!.Click += async (s, e) =>
        {
            ShowBanner(GetString(Resource.String.btn_discover) + "...");
            _discoveredDevices = await TVDiscovery.SearchAsync(this);
            var titles = _discoveredDevices.Select(d => $"{d.Name} ({d.Host})").ToList();
            _listDevices!.Adapter = new ArrayAdapter<string>(this, Android.Resource.Layout.SimpleListItem1, titles);
            ShowBanner(_discoveredDevices.Count > 0 ? $"{_discoveredDevices.Count} TV(s) LG!" : GetString(Resource.String.dialog_discovery_none));
        };

        _listDevices!.ItemClick += (s, e) =>
        {
            if (e.Position >= 0 && e.Position < _discoveredDevices.Count)
            {
                var d = _discoveredDevices[e.Position];
                _editIp.Text = d.Host;
                _editName.Text = d.Name;
                SaveConfig(d.Host, d.Name, d.Model, "");
                ConnectToTV();
            }
        };

        _btnSaveConnect!.Click += (s, e) =>
        {
            string host = _editIp.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(host))
            {
                ShowBanner(GetString(Resource.String.dialog_ip_required));
                return;
            }
            string name = _editName.Text?.Trim() ?? "LG webOS TV";
            SaveConfig(host, name, _currentModel, "");
            ConnectToTV();
        };

        _btnForget!.Click += (s, e) =>
        {
            _client?.Disconnect();
            _prefs?.Edit()?.Remove($"client_key_{_currentHost}")?.Apply();
            ShowBanner(GetString(Resource.String.dialog_key_forgotten));
            UpdateConnectionStatus(false, GetString(Resource.String.status_disconnected));
        };
    }

    private void SetAppLanguage(string lang)
    {
        _prefs?.Edit()?.PutString("app_language", lang)?.Apply();

        var locale = new Java.Util.Locale(lang);
        Java.Util.Locale.Default = locale;

        var config = new Android.Content.Res.Configuration(Resources?.Configuration);
        config.SetLocale(locale);
        Resources?.UpdateConfiguration(config, Resources.DisplayMetrics);

        Recreate();
    }

    private void SaveConfig(string host, string name, string model, string mac)
    {
        _currentHost = host;
        _currentName = name;
        _currentModel = model;
        _currentMac = mac;

        _prefs?.Edit()?
            .PutString("tv_host", host)?
            .PutString("tv_name", name)?
            .PutString("tv_model", model)?
            .PutString("tv_mac", mac)?
            .Apply();

        _tvTitle!.Text = _currentName;
    }

    private void LoadAppCatalog()
    {
        try
        {
            using var stream = Assets?.Open("app-catalog.json");
            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                var node = JsonNode.Parse(reader.ReadToEnd())?.AsArray();
                if (node != null)
                {
                    _allApps = node.Select(n => new TVApp(
                        n?["id"]?.GetValue<string>() ?? "",
                        n?["title"]?.GetValue<string>() ?? "",
                        n?["icon"]?.GetValue<string>()
                    )).OrderBy(a => a.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
                    UpdateAppsListView();
                }
            }
        }
        catch { }
    }

    private void ConnectToTV()
    {
        if (string.IsNullOrEmpty(_currentHost)) return;

        _client?.Dispose();
        UpdateConnectionStatus(false, GetString(Resource.String.banner_connecting));
        string? storedKey = _prefs?.GetString($"client_key_{_currentHost}", null);
        _client = new WebOSClient(this, _currentHost, storedKey, this);
        _ = _client.ConnectAsync();
    }

    private void HandlePower()
    {
        if (_client?.IsConnected == true)
        {
            _ = _client.TurnOffAsync();
            ShowBanner(GetString(Resource.String.banner_turning_off));
            UpdateConnectionStatus(false, GetString(Resource.String.status_disconnected));
        }
        else
        {
            ShowBanner(GetString(Resource.String.banner_connecting));
            ConnectToTV();
        }
    }

    private async void RefreshTVData()
    {
        if (_client == null || !_client.IsConnected) return;

        try
        {

            var apps = await _client.ListAppsAsync();
            if (apps.Count > 0)
            {
                _allApps = apps.OrderBy(a => a.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
                UpdateAppsListView();
            }

            _allInputs = await _client.GetInputsAsync();
            var inputLabels = _allInputs.Select(i => $"{i.Label} {(i.Connected ? GetString(Resource.String.input_connected) : "")}").ToList();
            _inputsAdapter = new ArrayAdapter<string>(this, Android.Resource.Layout.SimpleListItem1, inputLabels);
            _listInputs!.Adapter = _inputsAdapter;

            var pic = await _client.GetPictureSettingsAsync();
            _seekBacklight!.Progress = pic.Backlight;
            _seekBrightness!.Progress = pic.Brightness;
            _seekContrast!.Progress = pic.Contrast;
            _seekColor!.Progress = pic.Color;
        }
        catch (Exception ex)
        {
            ShowBanner($"Error: {ex.Message}");
        }
    }

    private void UpdateAppsListView()
    {
        _allApps = _allApps.OrderBy(a => a.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
        _displayedApps = new List<TVApp>(_allApps);
        var titles = _displayedApps.Select(a => a.Title).ToList();
        _appsAdapter = new ArrayAdapter<string>(this, Android.Resource.Layout.SimpleListItem1, titles);
        _listApps!.Adapter = _appsAdapter;
    }

    private void UpdateConnectionStatus(bool connected, string text)
    {
        RunOnUiThread(() =>
        {
            _tvStatus!.Text = text;
            var txtCardTitle = FindViewById<TextView>(Resource.Id.txt_power_card_title);
            var txtCardSub = FindViewById<TextView>(Resource.Id.txt_power_card_sub);

            if (connected)
            {
                _imgStatusDot?.SetImageResource(Resource.Drawable.shape_dot_green);
                if (txtCardTitle != null) txtCardTitle.Text = GetString(Resource.String.power_card_title_connected);
                if (txtCardSub != null) txtCardSub.Text = GetString(Resource.String.power_card_sub_connected);
                if (_btnWakeTv != null) _btnWakeTv.Text = GetString(Resource.String.btn_turn_off);
                if (_cardWakeTv != null) _cardWakeTv.Visibility = ViewStates.Visible;
                if (_txtCurrentApp != null) _txtCurrentApp.Visibility = ViewStates.Visible;
            }
            else
            {
                if (text.Contains("Ligando") || text.Contains("Conectando") || text.Contains("Connecting") || text.Contains("Pareando") || text.Contains("Pairing") || text.Contains("Emparejando") || text.Contains("Conectando"))
                {
                    _imgStatusDot?.SetImageResource(Resource.Drawable.shape_dot_amber);
                }
                else
                {
                    _imgStatusDot?.SetImageResource(Resource.Drawable.shape_dot_red);
                }
                if (txtCardTitle != null) txtCardTitle.Text = GetString(Resource.String.power_card_title_disconnected);
                if (txtCardSub != null) txtCardSub.Text = GetString(Resource.String.power_card_sub_disconnected);
                if (_btnWakeTv != null) _btnWakeTv.Text = GetString(Resource.String.btn_reconnect);
                if (_cardWakeTv != null) _cardWakeTv.Visibility = ViewStates.Visible;
                if (_txtCurrentApp != null) _txtCurrentApp.Visibility = ViewStates.Gone;
            }
        });
    }

    private void ShowBanner(string message)
    {
        RunOnUiThread(() =>
        {
            _txtBanner!.Text = message;
            _txtBanner.Visibility = ViewStates.Visible;
        });
    }

    // WebOSClient.IListener implementations
    public void OnConnected()
    {
        RunOnUiThread(() =>
        {
            UpdateConnectionStatus(true, GetString(Resource.String.status_connected));
            ShowBanner(GetString(Resource.String.banner_connected));
            RefreshTVData();

            // Success popup
            var dialog = new AndroidX.AppCompat.App.AlertDialog.Builder(this)
                .SetTitle(GetString(Resource.String.dialog_success_title) ?? "")
                .SetMessage(GetString(Resource.String.dialog_success_msg) ?? "")
                .SetPositiveButton("OK", (s, e) => { })
                .Create();
            dialog?.Show();
        });
    }

    public void OnPairingRequested()
    {
        RunOnUiThread(() =>
        {
            UpdateConnectionStatus(false, GetString(Resource.String.status_pairing));
            ShowBanner(GetString(Resource.String.banner_accept_pairing));
        });
    }

    public void OnPaired(string clientKey)
    {
        _prefs?.Edit()?.PutString($"client_key_{_currentHost}", clientKey)?.Apply();
    }

    public void OnDisconnected(string? reason)
    {
        RunOnUiThread(() =>
        {
            UpdateConnectionStatus(false, GetString(Resource.String.status_disconnected));
        });
    }

    public void OnVolumeChanged(int volume, bool muted)
    {
        RunOnUiThread(() =>
        {
            _currentVolume = volume;
            _isMuted = muted;
            _txtVolumeVal!.Text = muted ? GetString(Resource.String.btn_mute) : $"{volume}";
            _txtAudioStatus!.Text = muted ? GetString(Resource.String.audio_muted) : GetString(Resource.String.audio_speaker_vol);
            _seekVolume!.Progress = volume;
            _btnMute!.Text = muted ? GetString(Resource.String.btn_unmute) : GetString(Resource.String.btn_mute);
        });
    }

    public void OnForegroundAppChanged(string appId, string title)
    {
        RunOnUiThread(() =>
        {
            string displayTitle = !string.IsNullOrEmpty(title) ? title : _allApps.FirstOrDefault(a => a.Id == appId)?.Title ?? appId;
            _txtCurrentApp!.Text = $"{GetString(Resource.String.app_active_prefix)}{displayTitle}";
        });
    }

    public void OnPointerReady(bool ready)
    {
        RunOnUiThread(() =>
        {
            if (_client?.IsConnected == true)
            {
                UpdateConnectionStatus(true, ready ? GetString(Resource.String.status_ready) : GetString(Resource.String.status_connected));
            }
        });
    }

    public void OnError(string message)
    {
        ShowBanner(message);
    }

    protected override void OnDestroy()
    {
        _client?.Dispose();
        _client = null;
        base.OnDestroy();
    }
}