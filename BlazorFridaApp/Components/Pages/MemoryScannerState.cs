using System;
using System.Collections.Generic;
using System.Text.Json;
using BlazorFridaApp.MemoryScanner.Components;
using BlazorFridaApp.MemoryScanner.Models;

namespace BlazorFridaApp.Components.Pages
{
    public class MemoryScannerState
    {
        public List<ProcessInfo> ProcessList { get; set; } = new();
        public int? SelectedProcessId { get; set; }
        public string PatternHex { get; set; } = "";
        public string Mask { get; set; } = "";
        public List<IntPtr> ScanResults { get; set; } = new();
        public ScanType SelectedScanType { get; set; } = ScanType.ExactValue;
        public MemoryValueType SelectedValueType { get; set; } = MemoryValueType.Int32;
        public int SearchValue { get; set; }
        public bool IsFirstScan { get; set; } = true;
        public bool IsLoading { get; set; }
        public Dictionary<string, ScannerConfig> SavedConfigs { get; set; } = new();

        public Array ScanTypes => Enum.GetValues(typeof(ScanType));
        public Array ValueTypes => Enum.GetValues(typeof(MemoryValueType));

        public bool CanScan =>
            SelectedProcessId.HasValue &&
            SelectedValueType != MemoryValueType.String && // Strings not supported yet
            SelectedValueType != MemoryValueType.ByteArray &&  // Arrays not supported yet
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
                ValueType = SelectedValueType,
                ScanType = SelectedScanType.ToString(),
                ComparisonType = "exact",
                CustomSettings = new Dictionary<string, string>
                {
                    ["PatternHex"] = PatternHex,
                    ["Mask"] = Mask,
                    ["SearchValue"] = SearchValue.ToString()
                }
            };
        }

        public void LoadConfig(ScannerConfig config)
        {
            if (Enum.TryParse<ScanType>(config.ScanType, out var scanType))
            {
                SelectedScanType = scanType;
            }
            SelectedValueType = config.ValueType;
            PatternHex = config.CustomSettings?.GetValueOrDefault("PatternHex", "") ?? "";
            Mask = config.CustomSettings?.GetValueOrDefault("Mask", "") ?? "";
            if (int.TryParse(config.CustomSettings?.GetValueOrDefault("SearchValue", "0"), out var searchValue))
            {
                SearchValue = searchValue;
            }
            Reset();
        }
    }
}