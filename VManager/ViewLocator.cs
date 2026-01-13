using System;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using VManager.ViewModels;

namespace VManager;

public class ViewLocator : IDataTemplate
{
    [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with RequiresUnreferencedCodeAttribute",
        Justification = "Las vistas son preservadas via DynamicallyAccessedMembers en ViewModelBase")]
    [UnconditionalSuppressMessage("Trimming", "IL2072:Target parameter argument does not satisfy DynamicallyAccessedMembersAttribute",
        Justification = "Los tipos de vista son preservados a través de la preservación de ViewModelBase")]
    public Control? Build(object? data)
    {
        if (data is null)
            return null;

        // Obtener tipo de View de manera más segura
        var viewTypeName = data.GetType().FullName?.Replace("ViewModel", "View", StringComparison.Ordinal);
        if (string.IsNullOrEmpty(viewTypeName))
            return new TextBlock { Text = "Not Found: null type" };

        // Buscar en ensamblado conocido
        var assembly = typeof(ViewLocator).Assembly;
        var viewType = assembly.GetType(viewTypeName);

        if (viewType != null && typeof(Control).IsAssignableFrom(viewType))
        {
            var control = (Control?)Activator.CreateInstance(viewType);
            if (control != null)
            {
                control.DataContext = data;
                return control;
            }
        }

        return new TextBlock { Text = "Not Found: " + viewTypeName };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}