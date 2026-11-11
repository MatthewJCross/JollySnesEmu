using JollySnesEmu.Cartridge;
using JollySnesEmu.Game;
using Microsoft.Win32;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;

namespace JollySnesEmu;

public partial class MainWindow : Window
{
    private SnesCartridge? _cartridge;
    private JollySnesEmuGame? _game;

    public MainWindow()
    {
        InitializeComponent();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await GameView.WaitForReadyAsync();

            _game = new JollySnesEmuGame(GameView);
            _game.Start();

            StatusText.Text = "Jolly2DEngine initialised. Ready to load a cartridge.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Graphics initialisation failed.";
            MessageBox.Show(this, ex.Message, "JollySnesEmu", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }


    private async void LoadRom_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Load SNES cartridge",
            Filter = "SNES ROM images (*.sfc;*.smc)|*.sfc;*.smc|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) != true)
            return;

        if (_game is null)
        {
            MessageBox.Show(this, "The SNES game has not been initialized.", "JollySnesEmu", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            StatusText.Text = "Loading cartridge...";

            SnesCartridge cartridge = await SnesCartridge.LoadAsync(dialog.FileName);

            _game.InsertCartridge(cartridge);
            _cartridge = cartridge;

            RomNameText.Text = System.IO.Path.GetFileName(cartridge.FilePath);
            StatusText.Text = $"Loaded {cartridge.Info.Title} - {cartridge.Info.Mapping}";

            Debug.WriteLine($"ROM: {cartridge.Info.Title}");
            Debug.WriteLine($"Mapping: {cartridge.Info.Mapping}");
            Debug.WriteLine($"ROM size: {cartridge.Length:N0} bytes");
            Debug.WriteLine($"Reset vector: ${cartridge.ReadByte(0x7FFD):X2}{cartridge.ReadByte(0x7FFC):X2}");
        }
        catch (Exception ex)
        {
            StatusText.Text = "Cartridge load failed.";
            MessageBox.Show(this, ex.Message, "JollySnesEmu", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_game is null)
            return;

        _game.Stop();
        ((IDisposable)_game).Dispose();
        _game = null;
    }
}
