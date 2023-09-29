using Serious.Filters;
using Xunit;

public class FilterListTests
{
    public class TheNormalizeFiltersMethod
    {
        [Fact]
        public void CondensesExclusiveFilters()
        {
            var filterList = FilterParser.Parse("name:foo name:bar age:40 age:23 age:2");

            var result = filterList.NormalizeFilters(new[] { "age" });

            Assert.Equal("name:foo name:bar age:2", result.ToString().Trim());
        }
    }

    public class TheWithoutMethod
    {
        [Fact]
        public void RemovesFilterIfExists()
        {
            var filterList = new FilterList
            {
                Filter.Create("tag", "tag-value1"),
                Filter.Create("tag", "tag-value2"),
                Filter.Create("peanut", "butter"),
                Filter.Create("-tag", "tag-value"),
            };

            var result = filterList.Without("tag");

            var singleResult = Assert.Single(result);
            Assert.Equal(Filter.Create("peanut", "butter"), singleResult);
            Assert.Equal(4, filterList.Count); // Doesn't affect original list.
        }
    }

    public class TheWithReplacedMethod
    {
        [Fact]
        public void RemovesFilterIfExists()
        {
            var filterList = new FilterList
            {
                Filter.Create("tag", "tag-value1"),
                Filter.Create("tag", "tag-value2"),
                Filter.Create("peanut", "butter"),
                Filter.Create("-tag", "tag-value"),
            };

            var result = filterList.WithReplaced("tag", "new-tag-value").ToArray();


            Assert.Equal(new[] { Filter.Create("peanut", "butter"), Filter.Create("tag", "new-tag-value") }, result);
        }

        [Fact]
        public void CanReplaceWithNegationFilter()
        {
            var filterList = new FilterList
            {
                Filter.Create("tag", "tag-value1"),
                Filter.Create("tag", "tag-value2"),
                Filter.Create("peanut", "butter"),
                Filter.Create("-tag", "tag-value"),
            };

            var result = filterList.WithReplaced("-tag", "new-tag-value").ToArray();


            Assert.Equal(new[] { Filter.Create("peanut", "butter"), Filter.Create("-tag", "new-tag-value") }, result);
        }
    }

    public class TheWithDefaultsMethod
    {
        [Fact]
        public void ReturnsFilterListWithDefaultFilters()
        {
            FilterList list = default;
            var withDefaults = list.WithDefaults(new[]
            {
                Filter.Create("tag", "tag-value1"),
                Filter.Create("room", "the room"),
            });

            Assert.Equal(
                new[] { Filter.Create("tag", "tag-value1"), Filter.Create("room", "the room") },
                withDefaults.ToArray());
        }

        [Fact]
        public void DoesNotOverwriteExistingFilter()
        {
            FilterList list = new FilterList
            {
                Filter.Create("tag", "tag-value0"),
            };
            var withDefaults = list.WithDefaults(new[]
            {
                Filter.Create("tag", "tag-value1"),
                Filter.Create("room", "the room"),
            });

            Assert.Equal(
                new[] { Filter.Create("tag", "tag-value0"), Filter.Create("room", "the room") },
                withDefaults.ToArray());
        }
    }
}
