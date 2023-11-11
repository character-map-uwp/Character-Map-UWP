using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Windows.UI.Xaml.Media;

[Foundation.Metadata.WebHostHidden]
public static class VisualTreeHelperExtensions
{
    /// <summary>
    /// Gets the first descendant that is of the given type.
    /// </summary>
    /// <remarks>
    /// Returns null if not found.
    /// </remarks>
    /// <typeparam name="T">Type of descendant to look for.</typeparam>
    /// <param name="start">The start object.</param>
    /// <returns></returns>
    public static T GetFirstDescendantOfType<T>(this DependencyObject start, bool applyTemplate = false) where T : DependencyObject
        => start.GetDescendantsOfType<T>(applyTemplate).FirstOrDefault();

    /// <summary>
    /// Gets the descendants of the given type.
    /// </summary>
    /// <typeparam name="T">Type of descendants to return.</typeparam>
    /// <param name="start">The start.</param>
    /// <returns></returns>
    public static IEnumerable<T> GetDescendantsOfType<T>(this DependencyObject start, bool applyTemplate = false) where T : DependencyObject
        => start.GetDescendants(applyTemplate).OfType<T>();

    /// <summary>
    /// Returns the first matching descendant of each child node.
    /// </summary>
    /// <param name="start"></param>
    /// <param name="predicate"></param>
    /// <returns></returns>
    public static IEnumerable<FrameworkElement> GetFirstLevelDescendants(this FrameworkElement start, Predicate<FrameworkElement> predicate = null)
    {
        return GetFirstLevelDescendantsOfType(start, predicate);
    }

    public static IEnumerable<T> GetFirstLevelDescendantsOfType<T>(this FrameworkElement start)
    {
        return GetFirstLevelDescendantsOfType<T>(start, null);
    }

    public static IEnumerable<T> GetFirstLevelDescendantsOfType<T>(this FrameworkElement start, Predicate<T> predicate)
    {
        var queue = new Queue<FrameworkElement>();
        var count = VisualTreeHelper.GetChildrenCount(start);

        for (int i = 0; i < count; i++)
        {
            if (VisualTreeHelper.GetChild(start, i) is FrameworkElement child)
            {
                if (child is T c && (predicate == null || predicate(c)))
                {
                    yield return c;
                    continue;
                }
                else
                {
                    queue.Enqueue(child);
                }
            };
        }

        while (queue.Count > 0)
        {
            var parent = queue.Dequeue();
            var count2 = VisualTreeHelper.GetChildrenCount(parent);

            for (int i = 0; i < count2; i++)
            {
                if (VisualTreeHelper.GetChild(parent, i) is FrameworkElement child)
                {
                    if (child is T c && (predicate == null || predicate(c)))
                    {
                        yield return c;
                        continue;
                    }
                    else
                    {
                        queue.Enqueue(child);
                    }
                };
            }
        }
    }

    /// <summary>
    /// Gets the descendants.
    /// </summary>
    /// <param name="start">The start.</param>
    /// <returns></returns>
    public static IEnumerable<DependencyObject> GetDescendants(this DependencyObject start, bool applyTemplate = false)
    {
        if (applyTemplate)
        {
            var st = start as Control;
            if (start is Control)
                ((Control)start).ApplyTemplate();
        }

        var queue = new Queue<DependencyObject>();
        var count = VisualTreeHelper.GetChildrenCount(start);

        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(start, i);

            if (applyTemplate && child is Control)
                ((Control)child).ApplyTemplate();

            yield return child;
            queue.Enqueue(child);
        }

        while (queue.Count > 0)
        {
            var parent = queue.Dequeue();
            var count2 = VisualTreeHelper.GetChildrenCount(parent);

            for (int i = 0; i < count2; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (applyTemplate && child is Control)
                    ((Control)child).ApplyTemplate();

                yield return child;
                queue.Enqueue(child);
            }
        }
    }

    /// <summary>
    /// Gets the child elements.
    /// </summary>
    /// <param name="parent">The parent element.</param>
    /// <returns></returns>
    public static IEnumerable<DependencyObject> GetChildren(this DependencyObject parent)
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);

        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
        }
    }

    public static T GetChildOfType<T>(this DependencyObject depObj) where T : DependencyObject
    {
        if (depObj == null) return null;

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
        {
            var child = VisualTreeHelper.GetChild(depObj, i);

            var result = (child as T) ?? GetChildOfType<T>(child);
            if (result != null) return result;
        }
        return null;
    }

    /// <summary>
    /// Gets the child elements sorted in render order (by ZIndex first, declaration order second).
    /// </summary>
    /// <param name="parent">The parent element.</param>
    /// <returns></returns>
    public static IEnumerable<DependencyObject> GetChildrenByZIndex(
        this DependencyObject parent)
    {
        int i = 0;
        var indexedChildren =
            parent.GetChildren().Cast<FrameworkElement>().Select(
            child => new { Index = i++, ZIndex = Canvas.GetZIndex(child), Child = child });

        return
            from indexedChild in indexedChildren
            orderby indexedChild.ZIndex, indexedChild.Index
            select indexedChild.Child;
    }

    /// <summary>
    /// Gets the first ancestor that is of the given type.
    /// </summary>
    /// <remarks>
    /// Returns null if not found.
    /// </remarks>
    /// <typeparam name="T">Type of ancestor to look for.</typeparam>
    /// <param name="start">The start.</param>
    /// <returns></returns>
    public static T GetFirstAncestorOfType<T>(this DependencyObject start) where T : DependencyObject => start.GetAncestorsOfType<T>().FirstOrDefault();

    /// <summary>
    /// Gets the ancestors of a given type.
    /// </summary>
    /// <typeparam name="T">Type of ancestor to look for.</typeparam>
    /// <param name="start">The start.</param>
    /// <returns></returns>
    public static IEnumerable<T> GetAncestorsOfType<T>(this DependencyObject start) where T : DependencyObject => start.GetAncestors().OfType<T>();

    /// <summary>
    /// Gets the ancestors.
    /// </summary>
    /// <param name="start">The start.</param>
    /// <returns></returns>
    public static IEnumerable<DependencyObject> GetAncestors(this DependencyObject start)
    {
        var parent = VisualTreeHelper.GetParent(start);

        while (parent != null)
        {
            yield return parent;
            parent = VisualTreeHelper.GetParent(parent);
        }
    }

    public static IEnumerable<DependencyObject> GetAncestorsUntil(this DependencyObject start, DependencyObject end)
    {
        var parent = VisualTreeHelper.GetParent(start);

        while (parent != null && parent != end)
        {
            yield return parent;
            parent = VisualTreeHelper.GetParent(parent);
        }

        yield return end;
    }

    /// <summary>
    /// Determines whether the specified DependencyObject is in visual tree.
    /// </summary>
    /// <remarks>
    /// Note that this might not work as expected if the object is in a popup.
    /// </remarks>
    /// <param name="dob">The DependencyObject.</param>
    /// <returns>
    ///   <c>true</c> if the specified DependencyObject is in visual tree ; otherwise, <c>false</c>.
    /// </returns>
    public static bool IsInVisualTree(this DependencyObject dob)
    {
        //TODO: consider making it work with Popups too.
        if (Window.Current == null)
        {
            // This may happen when a picker or CameraCaptureUI etc. is open.
            return false;
        }

        return Window.Current.Content != null && dob.GetAncestors().Contains(Window.Current.Content);
    }

    /// <summary>
    /// Checks to see whether an item is within the visible region of another (ideally a parent, but doesn't have to be)
    /// </summary>
    /// <param name="element"></param>
    /// <param name="relativeTo"></param>
    /// <returns></returns>
    public static bool IsInViewport(this FrameworkElement element, FrameworkElement relativeTo = null)
    {
        if (relativeTo == null)
            relativeTo = (FrameworkElement)Window.Current.Content;

        var rectContainer = GetBoundingRect(element, relativeTo);

        if (rectContainer.HasValue == false)
            return false;
        var rect = rectContainer.Value;

        // TODO : This may *I THINK* be logically faulty. What if an item extends the entire extent of the screen?
        bool isLeftIn = (rect.Left >= 0 && rect.Left < relativeTo.ActualWidth) || (rect.Left + rect.Width >= 0 && rect.Left + rect.Width < relativeTo.ActualWidth);
        bool isRightIn = rect.Right > 0 && rect.Right <= relativeTo.ActualWidth;
        bool isTopIn = (rect.Top >= 0 && rect.Top < relativeTo.ActualHeight) || (rect.Top + rect.Height >= 0 && rect.Top + rect.Height < relativeTo.ActualHeight);
        bool isBottomIn = rect.Bottom > 0 && rect.Bottom <= relativeTo.ActualHeight;

        return ((isLeftIn || isRightIn) && (isTopIn || isBottomIn));
    }

    /// <summary>
    /// Gets the bounding rectangle of a given element
    /// relative to a given other element or visual root
    /// if relativeTo is null or not specified.
    /// </summary>
    /// <param name="dob">The starting element.</param>
    /// <param name="relativeTo">The relative to element.</param>
    /// <returns></returns>
    /// <exception cref="System.InvalidOperationException">Element not in visual tree.</exception>
    public static Rect? GetBoundingRect(this FrameworkElement dob, FrameworkElement relativeTo = null)
    {
        if (relativeTo == null)
        {
            relativeTo = Window.Current.Content as FrameworkElement;
        }

        if (relativeTo == null)
        {
            return null;
        }

        if (dob == relativeTo)
        {
            return new Rect(0, 0, relativeTo.ActualWidth, relativeTo.ActualHeight);
        }

        var ancestors = dob.GetAncestors().ToArray();

        if (!ancestors.Contains(relativeTo))
        {
            return null;
        }

        var pos =
            dob
                .TransformToVisual(relativeTo)
                .TransformPoint(new Point());
        var pos2 =
            dob
                .TransformToVisual(relativeTo)
                .TransformPoint(
                    new Point(
                        dob.ActualWidth,
                        dob.ActualHeight));

        return new Rect(pos, pos2);
    }

    public static bool ContainsFocus(this UIElement element)
    {
        if (element == null)
            return false;

        if (!(FocusManager.GetFocusedElement() is UIElement focused))
            return false;

        if (focused == element)
            return true;

        return focused.GetAncestors().Any(a => a == element);
    }

    /// <summary>
    /// Gets the implementation root of the Control.
    /// </summary>
    /// <param name="dependencyObject">The DependencyObject.</param>
    /// <returns>Returns the implementation root or null.</returns>
    public static FrameworkElement GetImplementationRoot(DependencyObject dependencyObject)
    {
        return (1 == VisualTreeHelper.GetChildrenCount(dependencyObject)) ?
            VisualTreeHelper.GetChild(dependencyObject, 0) as FrameworkElement :
            null;
    }

    public static VisualStateGroup GetVisualStateGroup(this Control control, string groupName)
    {
        if (GetImplementationRoot(control) is FrameworkElement f
            && VisualStateManager.GetVisualStateGroups(f) is IList<VisualStateGroup> groups)
            return groups.FirstOrDefault(g => g.Name == groupName);

        return null;
    }

    public static VisualStateGroup GetVisualStateGroup(this FrameworkElement f, string groupName)
    {
        if (VisualStateManager.GetVisualStateGroups(f) is IList<VisualStateGroup> groups)
            return groups.FirstOrDefault(g => g.Name == groupName);

        return null;
    }

    public static VisualState GetState(this VisualStateGroup group, string stateName)
    {
        return group.States.FirstOrDefault(s => s.Name == stateName);
    }

}
