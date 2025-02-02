using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using BlazorFridaApp.MemoryScanner.Components;

namespace BlazorFridaApp.Components.Pages
{
    public class MemoryScannerState
    {
        public List<Process> ProcessList { get; set; } = new();
        public int? SelectedProcessId { get; set; }
        public string PatternHex { get; set; } = "";
        public string Mask { get; set; } = "";
        public List<IntPtr> ScanResults { get; set; } = new();
        public ScanType SelectedScanType { get; set; } = ScanType.ExactValue;
        public MemoryValueType SelectedValueType { get; set; } = MemoryValueType.Int;
        public int SearchValue { get; set; }
        public bool IsFirstScan { get; set; } = true;
        public bool IsLoading { get; set; }

        public Array ScanTypes => Enum.GetValues(typeof(ScanType));
        public Array ValueTypes => Enum.GetValues(typeof(MemoryValueType));

        public bool CanScan =>
            SelectedProcessId.HasValue &&
            SelectedValueType != MemoryValueType.String && // Strings not supported yet
            SelectedValueType != MemoryValueType.Array &&  // Arrays not supported yet
            (SelectedScanType == ScanType.Pattern ?
                !string.IsNullOrWhiteSpace(PatternHex) && !string.IsNullOrWhiteSpace(Mask) :
                SelectedScanType == ScanType.UnknownInitialValue ||
                SelectedValueType != default(MemoryValueType));

        public void Reset()
        {
            ScanResults.Clear();
            if (SelectedProcessId.HasValue)
            {
                IsFirstScan = true;
            }
        }

        public void OnScanComplete(List<IntPtr> results)
        {
            ScanResults = results;
            IsFirstScan = false;
        }

        public ScannerConfig ToConfig()
        {
            return new ScannerConfig
            {
                ScanType = SelectedScanType,
                ValueType = SelectedValueType,
                PatternHex = PatternHex,
                Mask = Mask,
                SearchValue = SearchValue
            };
        }

        public void LoadConfig(ScannerConfig config)
        {
            SelectedScanType = config.ScanType;
            SelectedValueType = config.ValueType;
            PatternHex = config.PatternHex;
            Mask = config.Mask;
            SearchValue = config.SearchValue;
            Reset();
        }
    }

    public class ScannerConfig
    {
        public ScanType ScanType { get; set; }
        public MemoryValueType ValueType { get; set; }
        public string PatternHex { get; set; } = "";
        public string Mask { get; set; } = "";
        public int SearchValue { get; set; }

        public string ToJson() => JsonSerializer.Serialize(this);
        public static ScannerConfig FromJson(string json)
        {
            var config = JsonSerializer.Deserialize<ScannerConfig>(json);
            return config ?? throw new JsonException("Failed to deserialize ScannerConfig");
        }
    }
}