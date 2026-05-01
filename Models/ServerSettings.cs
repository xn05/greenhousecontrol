using System.Text.Json.Serialization;

namespace Greenhouse_Control.Models
{
    public sealed class ServerSettings
    {
        [JsonPropertyName("light_on")]
        public bool LightOn { get; set; }

        [JsonPropertyName("auto_water_on")]
        public bool AutoWaterOn { get; set; }

        [JsonPropertyName("frequency_hours")]
        public int FrequencyHours { get; set; }

        [JsonPropertyName("dispense_seconds")]
        public int DispenseSeconds { get; set; }

        [JsonPropertyName("last_power_action")]
        public string? LastPowerAction { get; set; }
    }
}

