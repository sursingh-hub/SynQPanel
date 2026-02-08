using SynQPanel.Plugins;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace SynQPanel.Extras
{
    /// <summary>
    /// Weather Plugin for SynQPanel
    /// Provides comprehensive weather data including current conditions, forecasts,
    /// air quality, astronomy, and alerts using WeatherAPI.com service.
    /// </summary>
    public sealed class WeatherPlugin : BasePlugin
    {
        // Configuration
        private const string CONFIG_FILE = "weather_config.ini";
        private string? _apiKey;
        private string? _location;
        private bool _useFahrenheit = false;

        // HTTP client for API requests
        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        // Status tracking
        private DateTime _lastSuccessfulUpdate = DateTime.MinValue;
        private string _lastError = string.Empty;

        // ===================================================================
        // LOCATION & STATUS (Text + Numeric)
        // ===================================================================
        private readonly PluginText _cityName;
        private readonly PluginText _region;
        private readonly PluginText _country;
        private readonly PluginSensor _latitude;
        private readonly PluginSensor _longitude;
        private readonly PluginText _timezone;
        private readonly PluginText _localTime;
        private readonly PluginText _lastUpdate;
        private readonly PluginText _updateStatus;

        // ===================================================================
        // CURRENT WEATHER (Text + Numeric)
        // ===================================================================
        private readonly PluginSensor _temperature;
        private readonly PluginSensor _feelsLike;
        private readonly PluginText _condition;
        private readonly PluginSensor _conditionCode;
        private readonly PluginText _iconUrl;

        private readonly PluginSensor _humidity;
        private readonly PluginSensor _cloudCover;
        private readonly PluginSensor _dewPoint;

        private readonly PluginSensor _windSpeed;
        private readonly PluginSensor _windDirection;
        private readonly PluginText _windDirectionText;
        private readonly PluginSensor _windGust;

        private readonly PluginSensor _pressure;
        private readonly PluginSensor _precipitation;
        private readonly PluginSensor _visibility;
        private readonly PluginSensor _uvIndex;

        private readonly PluginSensor _isDay;

        // ===================================================================
        // AIR QUALITY
        // ===================================================================
        private readonly PluginSensor _aqiUS;
        private readonly PluginSensor _aqiUK;
        private readonly PluginSensor _pm2_5;
        private readonly PluginSensor _pm10;
        private readonly PluginSensor _co;
        private readonly PluginSensor _no2;
        private readonly PluginSensor _o3;
        private readonly PluginSensor _so2;

        // ===================================================================
        // ASTRONOMY (Text + Numeric)
        // ===================================================================
        private readonly PluginText _sunrise;
        private readonly PluginText _sunset;
        private readonly PluginText _moonrise;
        private readonly PluginText _moonset;
        private readonly PluginText _moonPhase;
        private readonly PluginSensor _moonIllumination;

        // ===================================================================
        // TODAY'S FORECAST
        // ===================================================================
        private readonly PluginSensor _maxTemp;
        private readonly PluginSensor _minTemp;
        private readonly PluginSensor _avgTemp;
        private readonly PluginSensor _maxWind;
        private readonly PluginSensor _totalPrecipitation;
        private readonly PluginSensor _totalSnow;
        private readonly PluginSensor _avgVisibility;
        private readonly PluginSensor _avgHumidity;
        private readonly PluginSensor _chanceOfRain;
        private readonly PluginSensor _chanceOfSnow;
        private readonly PluginSensor _uvIndexForecast;

        // ===================================================================
        // TOMORROW'S FORECAST
        // ===================================================================
        private readonly PluginSensor _tomorrowMaxTemp;
        private readonly PluginSensor _tomorrowMinTemp;
        private readonly PluginSensor _tomorrowAvgTemp;
        private readonly PluginText _tomorrowCondition;
        private readonly PluginText _tomorrowIconUrl;
        private readonly PluginSensor _tomorrowMaxWind;
        private readonly PluginSensor _tomorrowPrecipitation;
        private readonly PluginSensor _tomorrowChanceOfRain;
        private readonly PluginSensor _tomorrowChanceOfSnow;

        // ===================================================================
        // ICONS
        // ===================================================================
        private readonly PluginText _todayIconUrl;

        // Hourly forecast icons (next 6 hours)
        private readonly PluginText _hour1IconUrl;
        private readonly PluginText _hour1Time;
        private readonly PluginText _hour2IconUrl;
        private readonly PluginText _hour2Time;
        private readonly PluginText _hour3IconUrl;
        private readonly PluginText _hour3Time;
        private readonly PluginText _hour4IconUrl;
        private readonly PluginText _hour4Time;
        private readonly PluginText _hour5IconUrl;
        private readonly PluginText _hour5Time;
        private readonly PluginText _hour6IconUrl;
        private readonly PluginText _hour6Time;



        public override string? ConfigFilePath => Path.Combine(
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "",
            CONFIG_FILE
        );

        public override TimeSpan UpdateInterval => TimeSpan.FromMinutes(10);

        public WeatherPlugin()
            : base(
                "weather",
                "Weather",
                "Comprehensive weather information powered by WeatherAPI.com including current conditions, forecasts, air quality, and astronomy data."
            )
        {
            // Initialize Location & Status
            _cityName = new PluginText("city_name", "City Name", "-");
            _region = new PluginText("region", "Region", "-");
            _country = new PluginText("country", "Country", "-");
            _latitude = new PluginSensor("Latitude", 0, "°");
            _longitude = new PluginSensor("Longitude", 0, "°");
            _timezone = new PluginText("timezone", "Timezone", "-");
            _localTime = new PluginText("local_time", "Local Time", "-");
            _lastUpdate = new PluginText("last_update", "Last Update", "-");
            _updateStatus = new PluginText("status", "Status", "Not updated");

            // Initialize Current Weather
            _temperature = new PluginSensor("Temperature", 0, "°C");
            _feelsLike = new PluginSensor("Feels Like", 0, "°C");
            _condition = new PluginText("condition", "Condition", "-");
            _conditionCode = new PluginSensor("Condition Code", 0, "");
            _iconUrl = new PluginText("icon_url", "Icon URL", "-");

            _humidity = new PluginSensor("Humidity", 0, "%");
            _cloudCover = new PluginSensor("Cloud Cover", 0, "%");
            _dewPoint = new PluginSensor("Dew Point", 0, "°C");

            _windSpeed = new PluginSensor("Wind Speed", 0, "km/h");
            _windDirection = new PluginSensor("Wind Direction", 0, "°");
            _windDirectionText = new PluginText("wind_dir_text", "Wind Direction", "-");
            _windGust = new PluginSensor("Wind Gust", 0, "km/h");

            _pressure = new PluginSensor("Pressure", 0, "mb");
            _precipitation = new PluginSensor("Precipitation", 0, "mm");
            _visibility = new PluginSensor("Visibility", 0, "km");
            _uvIndex = new PluginSensor("UV Index", 0, "");

            _isDay = new PluginSensor("Is Day", 0, "");

            // Initialize Air Quality
            _aqiUS = new PluginSensor("AQI (US EPA)", 0, "");
            _aqiUK = new PluginSensor("AQI (UK DEFRA)", 0, "");
            _pm2_5 = new PluginSensor("PM2.5", 0, "μg/m³");
            _pm10 = new PluginSensor("PM10", 0, "μg/m³");
            _co = new PluginSensor("CO", 0, "μg/m³");
            _no2 = new PluginSensor("NO2", 0, "μg/m³");
            _o3 = new PluginSensor("O3", 0, "μg/m³");
            _so2 = new PluginSensor("SO2", 0, "μg/m³");

            // Initialize Astronomy
            _sunrise = new PluginText("sunrise", "Sunrise", "-");
            _sunset = new PluginText("sunset", "Sunset", "-");
            _moonrise = new PluginText("moonrise", "Moonrise", "-");
            _moonset = new PluginText("moonset", "Moonset", "-");
            _moonPhase = new PluginText("moon_phase", "Moon Phase", "-");
            _moonIllumination = new PluginSensor("Moon Illumination", 0, "%");

            // Initialize Today's Forecast
            _maxTemp = new PluginSensor("Max Temperature", 0, "°C");
            _minTemp = new PluginSensor("Min Temperature", 0, "°C");
            _avgTemp = new PluginSensor("Avg Temperature", 0, "°C");
            _maxWind = new PluginSensor("Max Wind", 0, "km/h");
            _totalPrecipitation = new PluginSensor("Total Precipitation", 0, "mm");
            _totalSnow = new PluginSensor("Total Snow", 0, "cm");
            _avgVisibility = new PluginSensor("Avg Visibility", 0, "km");
            _avgHumidity = new PluginSensor("Avg Humidity", 0, "%");
            _chanceOfRain = new PluginSensor("Chance of Rain", 0, "%");
            _chanceOfSnow = new PluginSensor("Chance of Snow", 0, "%");
            _uvIndexForecast = new PluginSensor("UV Index", 0, "");

            // Initialize Tomorrow's Forecast
            _tomorrowMaxTemp = new PluginSensor("Max Temperature", 0, "°C");
            _tomorrowMinTemp = new PluginSensor("Min Temperature", 0, "°C");
            _tomorrowAvgTemp = new PluginSensor("Avg Temperature", 0, "°C");
            _tomorrowCondition = new PluginText("tomorrow_condition", "Condition", "-");
            _tomorrowIconUrl = new PluginText("tomorrow_icon_url", "Icon URL", "-");
            _tomorrowMaxWind = new PluginSensor("Max Wind", 0, "km/h");
            _tomorrowPrecipitation = new PluginSensor("Total Precipitation", 0, "mm");
            _tomorrowChanceOfRain = new PluginSensor("Chance of Rain", 0, "%");
            _tomorrowChanceOfSnow = new PluginSensor("Chance of Snow", 0, "%");

           
            _todayIconUrl = new PluginText("today_icon_url", "Today Icon URL", "-");

            // Hourly icons
            _hour1IconUrl = new PluginText("hour1_icon_url", "Hour 1 Icon", "-");
            _hour1Time = new PluginText("hour1_time", "Hour 1 Time", "-");
            _hour2IconUrl = new PluginText("hour2_icon_url", "Hour 2 Icon", "-");
            _hour2Time = new PluginText("hour2_time", "Hour 2 Time", "-");
            _hour3IconUrl = new PluginText("hour3_icon_url", "Hour 3 Icon", "-");
            _hour3Time = new PluginText("hour3_time", "Hour 3 Time", "-");
            _hour4IconUrl = new PluginText("hour4_icon_url", "Hour 4 Icon", "-");
            _hour4Time = new PluginText("hour4_time", "Hour 4 Time", "-");
            _hour5IconUrl = new PluginText("hour5_icon_url", "Hour 5 Icon", "-");
            _hour5Time = new PluginText("hour5_time", "Hour 5 Time", "-");
            _hour6IconUrl = new PluginText("hour6_icon_url", "Hour 6 Icon", "-");
            _hour6Time = new PluginText("hour6_time", "Hour 6 Time", "-");





        }

        public override void Initialize()
        {
            LoadConfiguration();
        }

        public override void Load(List<IPluginContainer> containers)
        {
            // Location & Status
            var location = new PluginContainer("weather-location", "Weather · Location");
            location.Entries.Add(_cityName);
            location.Entries.Add(_region);
            location.Entries.Add(_country);
            location.Entries.Add(_latitude);
            location.Entries.Add(_longitude);
            location.Entries.Add(_timezone);
            location.Entries.Add(_localTime);
            location.Entries.Add(_lastUpdate);
            location.Entries.Add(_updateStatus);

            // Current Conditions
            var current = new PluginContainer("weather-current", "Weather · Current");
            current.Entries.Add(_temperature);
            current.Entries.Add(_feelsLike);
            current.Entries.Add(_condition);
            current.Entries.Add(_conditionCode);
            current.Entries.Add(_iconUrl);
            current.Entries.Add(_isDay);
            current.Entries.Add(_humidity);
            current.Entries.Add(_cloudCover);
            current.Entries.Add(_dewPoint);

            // Wind & Pressure
            var wind = new PluginContainer("weather-wind", "Weather · Wind & Pressure");
            wind.Entries.Add(_windSpeed);
            wind.Entries.Add(_windDirection);
            wind.Entries.Add(_windDirectionText);
            wind.Entries.Add(_windGust);
            wind.Entries.Add(_pressure);
            wind.Entries.Add(_precipitation);
            wind.Entries.Add(_visibility);
            wind.Entries.Add(_uvIndex);

            // Air Quality
            var airQuality = new PluginContainer("weather-air", "Weather · Air Quality");
            airQuality.Entries.Add(_aqiUS);
            airQuality.Entries.Add(_aqiUK);
            airQuality.Entries.Add(_pm2_5);
            airQuality.Entries.Add(_pm10);
            airQuality.Entries.Add(_co);
            airQuality.Entries.Add(_no2);
            airQuality.Entries.Add(_o3);
            airQuality.Entries.Add(_so2);

            // Astronomy
            var astronomy = new PluginContainer("weather-astro", "Weather · Astronomy");
            astronomy.Entries.Add(_sunrise);
            astronomy.Entries.Add(_sunset);
            astronomy.Entries.Add(_moonrise);
            astronomy.Entries.Add(_moonset);
            astronomy.Entries.Add(_moonPhase);
            astronomy.Entries.Add(_moonIllumination);

            // Today's Forecast
            var today = new PluginContainer("weather-today", "Weather · Today's Forecast");
            today.Entries.Add(_maxTemp);
            today.Entries.Add(_minTemp);
            today.Entries.Add(_avgTemp);
            today.Entries.Add(_maxWind);
            today.Entries.Add(_totalPrecipitation);
            today.Entries.Add(_totalSnow);
            today.Entries.Add(_avgVisibility);
            today.Entries.Add(_avgHumidity);
            today.Entries.Add(_chanceOfRain);
            today.Entries.Add(_chanceOfSnow);
            today.Entries.Add(_uvIndexForecast);
            today.Entries.Add(_todayIconUrl);

            // Tomorrow's Forecast
            var tomorrow = new PluginContainer("weather-tomorrow", "Weather · Tomorrow");
            tomorrow.Entries.Add(_tomorrowMaxTemp);
            tomorrow.Entries.Add(_tomorrowMinTemp);
            tomorrow.Entries.Add(_tomorrowAvgTemp);
            tomorrow.Entries.Add(_tomorrowCondition);
            tomorrow.Entries.Add(_tomorrowIconUrl);
            tomorrow.Entries.Add(_tomorrowMaxWind);
            tomorrow.Entries.Add(_tomorrowPrecipitation);
            tomorrow.Entries.Add(_tomorrowChanceOfRain);
            tomorrow.Entries.Add(_tomorrowChanceOfSnow);

            // After the tomorrow container, before containers.Add(location);
            var hourly = new PluginContainer("weather-hourly", "Weather · Hourly Forecast");
            hourly.Entries.Add(_hour1Time);
            hourly.Entries.Add(_hour1IconUrl);
            hourly.Entries.Add(_hour2Time);
            hourly.Entries.Add(_hour2IconUrl);
            hourly.Entries.Add(_hour3Time);
            hourly.Entries.Add(_hour3IconUrl);
            hourly.Entries.Add(_hour4Time);
            hourly.Entries.Add(_hour4IconUrl);
            hourly.Entries.Add(_hour5Time);
            hourly.Entries.Add(_hour5IconUrl);
            hourly.Entries.Add(_hour6Time);
            hourly.Entries.Add(_hour6IconUrl);



            containers.Add(location);
            containers.Add(current);
            containers.Add(wind);
            containers.Add(airQuality);
            containers.Add(astronomy);
            containers.Add(today);
            containers.Add(tomorrow);
            containers.Add(hourly);

        }

        [PluginAction("Get API Key")]
        public void OpenApiKeyPage()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://www.weatherapi.com/signup.aspx",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _lastError = $"Failed to open browser: {ex.Message}";
            }
        }

        [PluginAction("Edit Configuration")]
        public void OpenConfigFile()
        {
            try
            {
                if (!string.IsNullOrEmpty(ConfigFilePath))
                {
                    if (!File.Exists(ConfigFilePath))
                    {
                        CreateDefaultConfig();
                    }

                    Process.Start(new ProcessStartInfo
                    {
                        FileName = ConfigFilePath,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                _lastError = $"Failed to open config file: {ex.Message}";
            }
        }

        [PluginAction("Reload Configuration")]
        public void ReloadConfig()
        {
            LoadConfiguration();
        }

        public override void Update()
        {
            // Synchronous update not used - all updates are async
        }

        public override async Task UpdateAsync(CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(_apiKey) || string.IsNullOrEmpty(_location))
            {
                _updateStatus.Value = "Configuration required";
                return;
            }

            try
            {
                await FetchWeatherDataAsync(cancellationToken);
                _updateStatus.Value = "OK";
                _lastSuccessfulUpdate = DateTime.Now;
                _lastUpdate.Value = _lastSuccessfulUpdate.ToString("HH:mm:ss");
                _lastError = string.Empty;
            }
            catch (Exception ex)
            {
                _updateStatus.Value = "Error";
                _lastError = ex.Message;
            }
        }

        private async Task FetchWeatherDataAsync(CancellationToken cancellationToken)
        {
            string url = $"https://api.weatherapi.com/v1/forecast.json" +
                        $"?key={_apiKey}" +
                        $"&q={Uri.EscapeDataString(_location ?? "London")}" +
                        $"&days=2" +
                        $"&aqi=yes" +
                        $"&alerts=no";

            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();

            // 🔍 DEBUG: Save the full response to a file so we can inspect it
            try
            {
                var debugPath = Path.Combine(
                    Path.GetDirectoryName(ConfigFilePath ?? "") ?? "",
                    "weather_response.json"
                );
                File.WriteAllText(debugPath, json);
                System.Diagnostics.Debug.WriteLine($"[Weather] Response saved to: {debugPath}");
            }
            catch { }



            var data = JsonDocument.Parse(json);
            var root = data.RootElement;

            // Parse location data
            if (root.TryGetProperty("location", out var location))
            {
                _cityName.Value = location.GetProperty("name").GetString() ?? "-";
                _region.Value = location.GetProperty("region").GetString() ?? "-";
                _country.Value = location.GetProperty("country").GetString() ?? "-";
                _latitude.Value = (float)location.GetProperty("lat").GetDouble();
                _longitude.Value = (float)location.GetProperty("lon").GetDouble();
                _timezone.Value = location.GetProperty("tz_id").GetString() ?? "-";
                _localTime.Value = location.GetProperty("localtime").GetString() ?? "-";
            }

            // Parse current weather
            if (root.TryGetProperty("current", out var current))
            {
                ParseCurrentWeather(current);
            }

            // Parse forecast
            if (root.TryGetProperty("forecast", out var forecast))
            {
                if (forecast.TryGetProperty("forecastday", out var forecastDays))
                {
                    var daysArray = forecastDays.EnumerateArray().ToArray();

                    if (daysArray.Length > 0)
                    {
                        ParseTodayForecast(daysArray[0]);
                        JsonElement? tomorrow = daysArray.Length > 1 ? daysArray[1] : null;
                        ParseHourlyForecast(daysArray[0], tomorrow);
                    }

                    if (daysArray.Length > 1)
                    {
                        ParseTomorrowForecast(daysArray[1]);
                    }
                }
            }
        }

        private void ParseCurrentWeather(JsonElement current)
        {
            // Temperature
            _temperature.Value = _useFahrenheit
                ? (float)current.GetProperty("temp_f").GetDouble()
                : (float)current.GetProperty("temp_c").GetDouble();

            _feelsLike.Value = _useFahrenheit
                ? (float)current.GetProperty("feelslike_f").GetDouble()
                : (float)current.GetProperty("feelslike_c").GetDouble();

            _dewPoint.Value = _useFahrenheit
                ? (float)current.GetProperty("dewpoint_f").GetDouble()
                : (float)current.GetProperty("dewpoint_c").GetDouble();

            // Condition
            if (current.TryGetProperty("condition", out var condition))
            {
                _condition.Value = condition.GetProperty("text").GetString() ?? "-";
                _conditionCode.Value = condition.GetProperty("code").GetInt32();
                _iconUrl.Value = "https:" + (condition.GetProperty("icon").GetString() ?? "");
            }

            // Basic measurements
            _isDay.Value = current.GetProperty("is_day").GetInt32();
            _humidity.Value = current.GetProperty("humidity").GetInt32();
            _cloudCover.Value = current.GetProperty("cloud").GetInt32();

            // Wind
            _windSpeed.Value = (float)current.GetProperty("wind_kph").GetDouble();
            _windDirection.Value = current.GetProperty("wind_degree").GetInt32();
            _windDirectionText.Value = current.GetProperty("wind_dir").GetString() ?? "-";
            _windGust.Value = (float)current.GetProperty("gust_kph").GetDouble();

            // Pressure & Precipitation
            _pressure.Value = (float)current.GetProperty("pressure_mb").GetDouble();
            _precipitation.Value = (float)current.GetProperty("precip_mm").GetDouble();
            _visibility.Value = (float)current.GetProperty("vis_km").GetDouble();
            _uvIndex.Value = (float)current.GetProperty("uv").GetDouble();

            // Air Quality
            if (current.TryGetProperty("air_quality", out var airQuality))
            {
                if (airQuality.TryGetProperty("us-epa-index", out var aqiUS))
                    _aqiUS.Value = aqiUS.GetInt32();
                if (airQuality.TryGetProperty("gb-defra-index", out var aqiUK))
                    _aqiUK.Value = aqiUK.GetInt32();

                if (airQuality.TryGetProperty("pm2_5", out var pm25))
                    _pm2_5.Value = (float)pm25.GetDouble();
                if (airQuality.TryGetProperty("pm10", out var pm10))
                    _pm10.Value = (float)pm10.GetDouble();
                if (airQuality.TryGetProperty("co", out var co))
                    _co.Value = (float)co.GetDouble();
                if (airQuality.TryGetProperty("no2", out var no2))
                    _no2.Value = (float)no2.GetDouble();
                if (airQuality.TryGetProperty("o3", out var o3))
                    _o3.Value = (float)o3.GetDouble();
                if (airQuality.TryGetProperty("so2", out var so2))
                    _so2.Value = (float)so2.GetDouble();
            }
        }

        private void ParseTodayForecast(JsonElement day)
        {
            // Astronomy
            if (day.TryGetProperty("astro", out var astro))
            {
                _sunrise.Value = astro.GetProperty("sunrise").GetString() ?? "-";
                _sunset.Value = astro.GetProperty("sunset").GetString() ?? "-";
                _moonrise.Value = astro.GetProperty("moonrise").GetString() ?? "-";
                _moonset.Value = astro.GetProperty("moonset").GetString() ?? "-";
                _moonPhase.Value = astro.GetProperty("moon_phase").GetString() ?? "-";

                // Fix: moon_illumination is returned as an integer, not string
                if (astro.TryGetProperty("moon_illumination", out var moonIllum))
                {
                    _moonIllumination.Value = moonIllum.ValueKind == JsonValueKind.String
                        ? float.Parse(moonIllum.GetString() ?? "0")
                        : (float)moonIllum.GetDouble();
                }
            }

            // Day forecast
            if (day.TryGetProperty("day", out var dayData))
            {
                _maxTemp.Value = _useFahrenheit
                    ? (float)dayData.GetProperty("maxtemp_f").GetDouble()
                    : (float)dayData.GetProperty("maxtemp_c").GetDouble();

                _minTemp.Value = _useFahrenheit
                    ? (float)dayData.GetProperty("mintemp_f").GetDouble()
                    : (float)dayData.GetProperty("mintemp_c").GetDouble();

                _avgTemp.Value = _useFahrenheit
                    ? (float)dayData.GetProperty("avgtemp_f").GetDouble()
                    : (float)dayData.GetProperty("avgtemp_c").GetDouble();

                _maxWind.Value = (float)dayData.GetProperty("maxwind_kph").GetDouble();
                _totalPrecipitation.Value = (float)dayData.GetProperty("totalprecip_mm").GetDouble();

                if (dayData.TryGetProperty("totalsnow_cm", out var snow))
                    _totalSnow.Value = (float)snow.GetDouble();

                _avgVisibility.Value = (float)dayData.GetProperty("avgvis_km").GetDouble();
                _avgHumidity.Value = (float)dayData.GetProperty("avghumidity").GetDouble();

                // ✅ CORRECT: These are already 0-100 percentages
                if (dayData.TryGetProperty("daily_chance_of_rain", out var rainChance))
                    _chanceOfRain.Value = (float)rainChance.GetDouble();

                if (dayData.TryGetProperty("daily_chance_of_snow", out var snowChance))
                    _chanceOfSnow.Value = (float)snowChance.GetDouble();

                _uvIndexForecast.Value = (float)dayData.GetProperty("uv").GetDouble();
            }
        }

        private void ParseTomorrowForecast(JsonElement day)
        {
            if (day.TryGetProperty("day", out var dayData))
            {
                _tomorrowMaxTemp.Value = _useFahrenheit
                    ? (float)dayData.GetProperty("maxtemp_f").GetDouble()
                    : (float)dayData.GetProperty("maxtemp_c").GetDouble();

                _tomorrowMinTemp.Value = _useFahrenheit
                    ? (float)dayData.GetProperty("mintemp_f").GetDouble()
                    : (float)dayData.GetProperty("mintemp_c").GetDouble();

                _tomorrowAvgTemp.Value = _useFahrenheit
                    ? (float)dayData.GetProperty("avgtemp_f").GetDouble()
                    : (float)dayData.GetProperty("avgtemp_c").GetDouble();

                if (dayData.TryGetProperty("condition", out var condition))
                {
                    _tomorrowCondition.Value = condition.GetProperty("text").GetString() ?? "-";
                    _tomorrowIconUrl.Value = "https:" + (condition.GetProperty("icon").GetString() ?? "");
                }

                if (dayData.TryGetProperty("condition", out var todayCondition))
                {
                    _todayIconUrl.Value = "https:" + (todayCondition.GetProperty("icon").GetString() ?? "");
                }

                _tomorrowMaxWind.Value = (float)dayData.GetProperty("maxwind_kph").GetDouble();
                _tomorrowPrecipitation.Value = (float)dayData.GetProperty("totalprecip_mm").GetDouble();

                // ✅ CORRECT: These are already 0-100 percentages
                if (dayData.TryGetProperty("daily_chance_of_rain", out var rainChance))
                    _tomorrowChanceOfRain.Value = (float)rainChance.GetDouble();

                if (dayData.TryGetProperty("daily_chance_of_snow", out var snowChance))
                    _tomorrowChanceOfSnow.Value = (float)snowChance.GetDouble();
            }
        }

        private void ParseHourlyForecast(JsonElement todayDay, JsonElement? tomorrowDay)
        {
            var allUpcomingHours = new List<JsonElement>();
            var currentHour = DateTime.Now.Hour;

            // Get remaining hours from today
            if (todayDay.TryGetProperty("hour", out var todayHours))
            {
                var todayRemaining = todayHours.EnumerateArray()
                    .Where(h =>
                    {
                        var hourTime = h.GetProperty("time").GetString() ?? "";
                        var hour = int.Parse(hourTime.Split(' ')[1].Split(':')[0]);
                        return hour >= currentHour;
                    })
                    .ToList();

                allUpcomingHours.AddRange(todayRemaining);
            }

            // If we need more hours, get from tomorrow
            if (allUpcomingHours.Count < 6 && tomorrowDay.HasValue)
            {
                if (tomorrowDay.Value.TryGetProperty("hour", out var tomorrowHours))
                {
                    var needed = 6 - allUpcomingHours.Count;
                    allUpcomingHours.AddRange(tomorrowHours.EnumerateArray().Take(needed));
                }
            }

            // Assign to fields
            if (allUpcomingHours.Count > 0) ParseHourData(allUpcomingHours[0], _hour1Time, _hour1IconUrl);
            if (allUpcomingHours.Count > 1) ParseHourData(allUpcomingHours[1], _hour2Time, _hour2IconUrl);
            if (allUpcomingHours.Count > 2) ParseHourData(allUpcomingHours[2], _hour3Time, _hour3IconUrl);
            if (allUpcomingHours.Count > 3) ParseHourData(allUpcomingHours[3], _hour4Time, _hour4IconUrl);
            if (allUpcomingHours.Count > 4) ParseHourData(allUpcomingHours[4], _hour5Time, _hour5IconUrl);
            if (allUpcomingHours.Count > 5) ParseHourData(allUpcomingHours[5], _hour6Time, _hour6IconUrl);
        }



        private void ParseHourData(JsonElement hour, PluginText timeField, PluginText iconField)
        {
            timeField.Value = hour.GetProperty("time").GetString()?.Split(' ')[1] ?? "-";

            if (hour.TryGetProperty("condition", out var condition))
            {
                iconField.Value = "https:" + (condition.GetProperty("icon").GetString() ?? "");
            }
        }





        private void LoadConfiguration()
        {
            try
            {
                if (string.IsNullOrEmpty(ConfigFilePath) || !File.Exists(ConfigFilePath))
                {
                    CreateDefaultConfig();
                    return;
                }

                var lines = File.ReadAllLines(ConfigFilePath);
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"))
                        continue;

                    var parts = line.Split('=', 2);
                    if (parts.Length != 2)
                        continue;

                    var key = parts[0].Trim();
                    var value = parts[1].Trim();

                    switch (key.ToLower())
                    {
                        case "apikey":
                            _apiKey = value;
                            break;
                        case "location":
                            _location = value;
                            break;
                        case "usefahrenheit":
                            _useFahrenheit = value.ToLower() == "true" || value == "1";
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                _lastError = $"Config load error: {ex.Message}";
            }
        }

        private void CreateDefaultConfig()
        {
            try
            {
                if (string.IsNullOrEmpty(ConfigFilePath))
                    return;

                var defaultConfig = @"# SynQPanel Weather Plugin Configuration
                # Get your free API key from: https://www.weatherapi.com/signup.aspx

                # Your WeatherAPI.com API Key (Required)
                APIKey=YOUR_API_KEY_HERE

                # Location (city name, zip code, coordinates, IP address)
                # Examples: London, New Delhi, 10001, 48.8567,2.3508, auto:ip
                Location=London

                # Temperature Unit (true for Fahrenheit, false for Celsius)
                UseFahrenheit=false
                ";

                File.WriteAllText(ConfigFilePath, defaultConfig);
            }
            catch (Exception ex)
            {
                _lastError = $"Config creation error: {ex.Message}";
            }
        }

        public override void Close()
        {
            // Cleanup if needed
        }

    }
}