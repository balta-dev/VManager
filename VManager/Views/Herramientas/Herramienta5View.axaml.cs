using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using VManager.Models;
using VManager.ViewModels;
using VManager.ViewModels.Herramientas;

namespace VManager.Views.Herramientas
{
    public partial class Herramienta5View : SoundEnabledUserControl
    {
        public Herramienta5View()
        {
            InitializeComponent();
            
            var listBox = this.FindControl<ListBox>("VideoListBox");
            if (listBox != null)
            {
                listBox.PointerWheelChanged += ListBox_PointerWheelChanged;

                // Suscribirse a cambios de la colección para hookear PropertyChanged en cada item nuevo
                if (DataContext is Herramienta5ViewModel vm)
                    HookViewModel(vm);

                DataContextChanged += (_, _) =>
                {
                    if (DataContext is Herramienta5ViewModel newVm)
                        HookViewModel(newVm);
                };
            }
        }

        private void HookViewModel(Herramienta5ViewModel vm)
        {
            // Cuando se agrega un video nuevo a la lista, suscribirse a sus cambios de propiedad
            vm.Videos.CollectionChanged += (_, e) =>
            {
                if (e.NewItems == null) return;
                foreach (VideoDownloadItem item in e.NewItems)
                {
                    item.PropertyChanged += (sender, args) =>
                    {
                        // Solo nos importa el cambio de SelectedFormat en items de playlist
                        if (args.PropertyName != nameof(VideoDownloadItem.SelectedFormat))
                            return;
                        if (sender is not VideoDownloadItem changedItem)
                            return;
                        if (changedItem.PlaylistId == null)
                            return;

                        vm.OnPlaylistItemFormatChanged(changedItem);
                    };
                }
            };
        }

        private void ListBox_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            if (sender is not ListBox listBox) return;

            var scrollViewer = listBox.Scroll as ScrollViewer;

            if (scrollViewer == null)
            {
                e.Handled = false;
                return;
            }

            bool hasScrollableContent = scrollViewer.Extent.Height > scrollViewer.Viewport.Height + 1;

            if (!hasScrollableContent)
            {
                e.Handled = false;
                return;
            }

            e.Handled = true;
        }
        
        private void DownloadHelpBorder_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (DataContext is Herramienta5ViewModel vm)
            {
                vm.ShowDownloadHelp = false;
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
