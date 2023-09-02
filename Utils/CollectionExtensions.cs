using Microsoft.AspNetCore.Mvc.Rendering;

namespace GeotecnologiaKNS.Utils
{
    public static class CollectionExtensions
    {
        public static IEnumerable<SelectListItem> ToSelectListItems<T>(
           this IEnumerable<T> values,
           Func<T, object> text,
           Func<T, object> value,
           Action<SelectListItemsOptions>? options = default)
        {
            SelectListItemsOptions selectListItemsOptions = new();
            options?.Invoke(selectListItemsOptions);

            if (selectListItemsOptions.Placeholder != null)
            {
                var source = new List<SelectListItem> { new SelectListItem(selectListItemsOptions.Placeholder, "") };
                source.AddRange(values.Select(x => new SelectListItem(
                                     text(x).ToString(),
                                     value(x).ToString(),
                                     selectListItemsOptions.Selected,
                                     selectListItemsOptions.Disabled)));
                return source;
            }

            return values.Select(x => new SelectListItem(
                text(x).ToString(),
                value(x).ToString(),
                selectListItemsOptions.Selected,
                selectListItemsOptions.Disabled));
        }

        public class SelectListItemsOptions
        {
            public bool Selected { get; set; }
            public bool Disabled { get; set; }
            public string? Placeholder { get; set; }
        }
    }
}
