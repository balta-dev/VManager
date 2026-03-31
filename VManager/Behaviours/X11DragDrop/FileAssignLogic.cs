using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks; // <--- 1. NUEVO: Para usar Task
using DynamicData;
using ReactiveUI;

namespace VManager.Behaviours.X11DragDrop;

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
public static class FileAssignLogic
{
    // 2. CAMBIO: Ahora es 'async Task<bool>' en lugar de 'bool'
    public static async Task<bool> AssignVideoFiles(
        object dataContext,
        IEnumerable<string> paths)
    {
        var validPaths = paths.ToList();
        if (!validPaths.Any())
            return false;

        var dcType = dataContext.GetType();
        var listProp = dcType.GetProperty("VideoPaths");
        var singleProp = dcType.GetProperty("VideoPath");
        
        // Buscamos tu método de ffprobe en el ViewModel
        var loadDurationMethod = dcType.GetMethod("LoadVideoDurationAsync");

        if (listProp?.CanWrite == true)
        {
            if (listProp.GetValue(dataContext) is ObservableCollection<string> list)
            {
                var toAdd = validPaths.Except(list).ToList();
                if (toAdd.Count > 0) list.AddRange(toAdd);
            }
            else
                listProp.SetValue(dataContext, new ObservableCollection<string>(validPaths));

            if (dataContext is ReactiveObject ro)
                ro.RaisePropertyChanged("VideoPaths");
        }

        if (singleProp?.CanWrite == true)
        {
            string firstPath = validPaths.First();
            singleProp.SetValue(dataContext, firstPath);
            
            if (dataContext is ReactiveObject ro)
            {
                ro.RaisePropertyChanged("VideoPath");
                
                // 3. NUEVO: Si encontramos tu método de ffprobe, lo ejecutamos
                if (loadDurationMethod != null)
                {
                    await (Task)loadDurationMethod.Invoke(dataContext, new object[] { firstPath })!;
                }
            }
        }

        return true;
    }
}