using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using DynamicData;
using ReactiveUI;

namespace VManager.Services;

public static class FileAssignLogic
{
    public static bool AssignVideoFiles(
        object dataContext,
        IEnumerable<string> paths)
    {
        var validPaths = paths.ToList();
        if (!validPaths.Any())
            return false;

        var dcType = dataContext.GetType();

        var listProp = dcType.GetProperty("VideoPaths");
        var singleProp = dcType.GetProperty("VideoPath");

        if (listProp?.CanWrite == true)
        {
            if (listProp.GetValue(dataContext) is ObservableCollection<string> list)
                list.AddRange(validPaths);
            else
                listProp.SetValue(dataContext, new ObservableCollection<string>(validPaths));

            if (dataContext is ReactiveUI.ReactiveObject ro)
                ((IReactiveObject)ro).RaisePropertyChanged("VideoPaths");
        }

        if (singleProp?.CanWrite == true)
        {
            singleProp.SetValue(dataContext, validPaths.First());
            if (dataContext is ReactiveUI.ReactiveObject ro)
                ((IReactiveObject)ro).RaisePropertyChanged("VideoPath");
        }

        return true;
    }
}
