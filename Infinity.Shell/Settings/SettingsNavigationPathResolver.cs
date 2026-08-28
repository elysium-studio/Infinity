namespace Infinity.Shell;

public sealed class SettingsNavigationPathResolver
{
    public IReadOnlyList<ISettingViewModel> GetInitialPath(IEnumerable<ISettingViewModel> roots)
    {
        ISettingViewModel? initial = roots.FirstOrDefault();

        if (initial is null)
        {
            return [];
        }

        return AppendDefaultChild([initial]);
    }

    public IReadOnlyList<ISettingViewModel> GetSelectionPath(IEnumerable<ISettingViewModel> roots, ISettingViewModel target)
    {
        ArgumentNullException.ThrowIfNull(target);

        foreach (ISettingViewModel root in roots)
        {
            List<ISettingViewModel> path = [];

            if (TryFindPath(root, target, path))
            {
                return AppendDefaultChild(path);
            }
        }

        return [];
    }

    public IReadOnlyList<ISettingViewModel> GetBreadcrumbPath(IReadOnlyList<ISettingViewModel> currentPath, int breadcrumbIndex)
    {
        if (breadcrumbIndex < 0 || breadcrumbIndex >= currentPath.Count - 1)
        {
            return currentPath;
        }

        return AppendDefaultChild([.. currentPath.Take(breadcrumbIndex + 1)]);
    }

    public IReadOnlyList<ISettingViewModel> GetBackPath(IReadOnlyList<ISettingViewModel> currentPath)
        => currentPath.Count < 2 ? currentPath : [.. currentPath.Take(currentPath.Count - 1)];

    private static IReadOnlyList<ISettingViewModel> AppendDefaultChild(List<ISettingViewModel> path)
    {
        if (path.Count > 0 && path[^1].Children.Count > 0)
        {
            path.Add(path[^1].Children[0]);
        }

        return path;
    }

    private static bool TryFindPath(ISettingViewModel current, ISettingViewModel target, List<ISettingViewModel> path)
    {
        path.Add(current);

        if (ReferenceEquals(current, target))
        {
            return true;
        }

        foreach (ISettingViewModel child in current.Children)
        {
            if (TryFindPath(child, target, path))
            {
                return true;
            }
        }

        path.RemoveAt(path.Count - 1);
        return false;
    }
}
