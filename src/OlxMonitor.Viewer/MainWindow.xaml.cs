using Microsoft.EntityFrameworkCore;
using OlxMonitor.Core.Models;
using OlxMonitor.Infrastructure.Data;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace OlxMonitor.Viewer;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<Listing> _data = new();

    public MainWindow()
    {
        InitializeComponent();
        Grid.ItemsSource = _data;
        Loaded += async (s, e) => await Load();
    }

    private async Task Load()
    {
        try
        {
            var dbPath = @"S:\Studia2\Projekt-Semestralny-1\Marketplace-Monitor-Project\src\OlxMonitor.Worker\Data\olxmonitor.db";

            if (!File.Exists(dbPath))
            {
                MessageBox.Show("Database not found.\n\nRun the monitor (Worker) first.", "Missing DB", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var options = new DbContextOptionsBuilder<OlxMonitorDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;

            using var ctx = new OlxMonitorDbContext(options);
            await ctx.Database.EnsureCreatedAsync();

            var list = await ctx.Listings
                .OrderByDescending(x => x.FirstSeen)
                .ToListAsync();

            _data.Clear();
            foreach (var item in list)
                _data.Add(item);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void RefreshBtn_Click(object sender, RoutedEventArgs e)
    {
        await Load();
    }

    private void Url_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is TextBlock tb && tb.Text is string url && !string.IsNullOrEmpty(url))
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch { }
        }
    }
}