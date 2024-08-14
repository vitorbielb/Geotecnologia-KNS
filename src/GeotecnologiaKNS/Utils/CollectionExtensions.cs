using Microsoft.AspNetCore.Mvc.Rendering;

namespace GeotecnologiaKNS.Utils
{
    public static class CollectionExtensions
    {
        public static IEnumerable<SelectListItem> ToSelectListItems<T>(
            this IEnumerable<T> values,
            Func<T, object?> text,
            Func<T, object?> value,
            Action<SelectListItemsOptions>? options = null)
        {
            ArgumentNullException.ThrowIfNull(values);
            ArgumentNullException.ThrowIfNull(text);
            ArgumentNullException.ThrowIfNull(value);

            var selectListOptions = new SelectListItemsOptions();
            options?.Invoke(selectListOptions);

            var items = values.Select(x => new SelectListItem(
                text: text(x)?.ToString() ?? string.Empty,
                value: value(x)?.ToString() ?? string.Empty,
                selected: selectListOptions.Selected,
                disabled: selectListOptions.Disabled))
                .ToList();

            if (!string.IsNullOrWhiteSpace(selectListOptions.Placeholder))
            {
                items.Insert(0, new SelectListItem(selectListOptions.Placeholder, string.Empty));
            }

            return items;
        }

        public sealed class SelectListItemsOptions
        {
            public bool Selected { get; set; }
            public bool Disabled { get; set; }
            public string? Placeholder { get; set; }
        }
    }
}