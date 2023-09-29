using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Serious.Collections;
using Serious.TestHelpers;
using Xunit;

public class PaginatedListTests
{
    public class ThePagesProperty
    {
        [Fact]
        public void IteratesSinglePage()
        {
            int totalCount = 3;
            int pageNumber = 1;
            int pageSize = 3;
            var elements = Enumerable.Repeat(0, totalCount);
            var paginatedList = PaginatedList.CreateList(elements, totalCount, pageNumber, pageSize);

            var pages = paginatedList.Pages.ToList();

            Assert.Equal(3, paginatedList.TotalCount);
            Assert.Equal(1, paginatedList.TotalPages);
            Assert.Equal(1, Assert.IsType<PaginatedPage>(pages[0]).Number);
            Assert.Single(pages);
        }

        [Fact]
        public void IteratesAllPages()
        {
            int totalCount = 3;
            int pageNumber = 1;
            int pageSize = 1;
            var elements = Enumerable.Repeat(0, totalCount);
            var paginatedList = PaginatedList.CreateList(elements, totalCount, pageNumber, pageSize);

            var pages = paginatedList.Pages.ToList();

            Assert.Equal(1, Assert.IsType<PaginatedPage>(pages[0]).Number);
            Assert.Equal(2, Assert.IsType<PaginatedPage>(pages[1]).Number);
            Assert.Equal(3, Assert.IsType<PaginatedPage>(pages[2]).Number);
            Assert.Equal(3, pages.Count);
        }

        [Fact]
        public void ShowsLastTwoPagesWhenNineOrMorePages()
        {
            int totalCount = 9;
            int pageNumber = 1;
            var elements = Enumerable.Repeat(0, totalCount);
            var paginatedList = PaginatedList.CreateList(elements, totalCount, pageNumber, 1);

            var pages = paginatedList.Pages.ToList();

            Assert.Equal(1, Assert.IsType<PaginatedPage>(pages[0]).Number);
            Assert.Equal(2, Assert.IsType<PaginatedPage>(pages[1]).Number);
            Assert.Equal(3, Assert.IsType<PaginatedPage>(pages[2]).Number);
            Assert.Equal(4, Assert.IsType<PaginatedPage>(pages[3]).Number);
            Assert.Equal(5, Assert.IsType<PaginatedPage>(pages[4]).Number);
            Assert.IsType<PaginatedDivider>(pages[5]);
            Assert.Equal(8, Assert.IsType<PaginatedPage>(pages[6]).Number);
            Assert.Equal(9, Assert.IsType<PaginatedPage>(pages[7]).Number);
            Assert.Equal(8, pages.Count); // [*1*],[2],[3],[4],[5],[…],[8],[9]
        }

        [Fact]
        public void ShowsTwoPagesAfterCurrentPage()
        {
            int totalCount = 10;
            int pageNumber = 4;
            var elements = Enumerable.Repeat(0, totalCount);
            var paginatedList = PaginatedList.CreateList(elements, totalCount, pageNumber, 1);

            var pages = paginatedList.Pages.ToList();

            Assert.IsType<PaginatedDivider>(pages[^3]);
            Assert.Equal(9, Assert.IsType<PaginatedPage>(pages[^2]).Number);
            Assert.Equal(10, Assert.IsType<PaginatedPage>(pages[^1]).Number);
            Assert.Equal(9, pages.Count); // [1],[2],[3],[*4*],[5],[6],[…],[9],[10]
        }

        [Fact]
        public void ShowsTwoPagesAfterCurrentPageSix()
        {
            int totalCount = 14;
            int pageNumber = 6;
            var elements = Enumerable.Repeat(0, totalCount);
            var paginatedList = PaginatedList.CreateList(elements, totalCount, pageNumber, 1);

            var pages = paginatedList.Pages.ToList();

            Assert.Equal(1, Assert.IsType<PaginatedPage>(pages[0]).Number);
            Assert.Equal(2, Assert.IsType<PaginatedPage>(pages[1]).Number);
            Assert.Equal(3, Assert.IsType<PaginatedPage>(pages[2]).Number);
            Assert.Equal(4, Assert.IsType<PaginatedPage>(pages[3]).Number);
            Assert.Equal(5, Assert.IsType<PaginatedPage>(pages[4]).Number);
            Assert.Equal(6, Assert.IsType<PaginatedPage>(pages[5]).Number);
            Assert.Equal(7, Assert.IsType<PaginatedPage>(pages[6]).Number);
            Assert.Equal(8, Assert.IsType<PaginatedPage>(pages[7]).Number);
            Assert.IsType<PaginatedDivider>(pages[8]);
            Assert.Equal(13, Assert.IsType<PaginatedPage>(pages[9]).Number);
            Assert.Equal(14, Assert.IsType<PaginatedPage>(pages[10]).Number);
            Assert.Equal(11, pages.Count);
        }

        [Fact]
        public void ShowsFirstTwoPagesFollowedByDividerWhenCurrentPageIsSevenOrMore()
        {
            int totalCount = 15;
            int pageNumber = 7;
            var elements = Enumerable.Repeat(0, totalCount);
            var paginatedList = PaginatedList.CreateList(elements, totalCount, pageNumber, 1);

            var pages = paginatedList.Pages.ToList();

            Assert.Equal(1, Assert.IsType<PaginatedPage>(pages[0]).Number);
            Assert.Equal(2, Assert.IsType<PaginatedPage>(pages[1]).Number);
            Assert.IsType<PaginatedDivider>(pages[2]);
            Assert.Equal(5, Assert.IsType<PaginatedPage>(pages[3]).Number);
            Assert.Equal(6, Assert.IsType<PaginatedPage>(pages[4]).Number);
            Assert.Equal(7, Assert.IsType<PaginatedPage>(pages[5]).Number);
            Assert.Equal(8, Assert.IsType<PaginatedPage>(pages[6]).Number);
            Assert.Equal(9, Assert.IsType<PaginatedPage>(pages[7]).Number);
            Assert.IsType<PaginatedDivider>(pages[8]);
            Assert.Equal(14, Assert.IsType<PaginatedPage>(pages[9]).Number);
            Assert.Equal(15, Assert.IsType<PaginatedPage>(pages[10]).Number);

            Assert.Equal(11, pages.Count); // [1],[2],[…],[5],[6],[*7*],[8],[9],[…],[14],[15]
        }

        [Fact]
        public void ShowsAllPagesWhenCurrentPageIsRightInMiddleOfElevenPages()
        {
            int totalCount = 11;
            int pageNumber = 6;
            int pageSize = 1;
            var elements = Enumerable.Repeat(0, totalCount);
            var paginatedList = PaginatedList.CreateList(elements, totalCount, pageNumber, pageSize);

            var pages = paginatedList.Pages.ToList();

            Assert.Equal(1, Assert.IsType<PaginatedPage>(pages[0]).Number);
            Assert.Equal(2, Assert.IsType<PaginatedPage>(pages[1]).Number);
            Assert.Equal(3, Assert.IsType<PaginatedPage>(pages[2]).Number);
            Assert.Equal(4, Assert.IsType<PaginatedPage>(pages[3]).Number);
            Assert.Equal(5, Assert.IsType<PaginatedPage>(pages[4]).Number);
            Assert.Equal(6, Assert.IsType<PaginatedPage>(pages[5]).Number);
            Assert.Equal(7, Assert.IsType<PaginatedPage>(pages[6]).Number);
            Assert.Equal(8, Assert.IsType<PaginatedPage>(pages[7]).Number);
            Assert.Equal(9, Assert.IsType<PaginatedPage>(pages[8]).Number);
            Assert.Equal(10, Assert.IsType<PaginatedPage>(pages[9]).Number);
            Assert.Equal(11, Assert.IsType<PaginatedPage>(pages[10]).Number);

            Assert.Equal(11, pages.Count); // [1],[2],[3],[4],[5],[*6*],[7],[8],[9],[10],[11]
        }

        [Fact]
        public void ShowsLastFivePages()
        {
            int totalCount = 15;
            int pageNumber = 14;
            int pageSize = 1;
            var elements = Enumerable.Repeat(0, totalCount);
            var paginatedList = PaginatedList.CreateList(elements, totalCount, pageNumber, pageSize);

            var pages = paginatedList.Pages.ToList();

            Assert.Equal(1, Assert.IsType<PaginatedPage>(pages[0]).Number);
            Assert.Equal(2, Assert.IsType<PaginatedPage>(pages[1]).Number);
            Assert.IsType<PaginatedDivider>(pages[2]);
            Assert.Equal(11, Assert.IsType<PaginatedPage>(pages[3]).Number);
            Assert.Equal(12, Assert.IsType<PaginatedPage>(pages[4]).Number);
            Assert.Equal(13, Assert.IsType<PaginatedPage>(pages[5]).Number);
            Assert.Equal(14, Assert.IsType<PaginatedPage>(pages[6]).Number);
            Assert.Equal(15, Assert.IsType<PaginatedPage>(pages[7]).Number);

            Assert.Equal(8, pages.Count); // [1],[2],[...],[11],[12],[13],[*14*],[15]
        }
    }

    public class TheTotalCountProperty
    {
        [Fact]
        public void RepresentsTotalCountWhenCreatedFromNonQueryable()
        {
            int totalCount = 15;
            int pageNumber = 14;
            int pageSize = 10;
            var elements = Enumerable.Repeat(0, pageSize);

            var paginatedList = PaginatedList.CreateList(elements, totalCount, pageNumber, pageSize);

            Assert.Equal(15, paginatedList.TotalCount);
            Assert.Equal(10, paginatedList.Count);
        }
    }

    public class TheCreateAsyncMethod
    {
        [Fact]
        public async Task CreatesPaginatedListFromQueryable()
        {
            var queryable = new TestAsyncEnumerable<int>(new[] { 1, 2, 3, 4, 5 }).AsQueryable();

            var list = await PaginatedList.CreateAsync(queryable, 1, 2);

            Assert.Equal(5, list.TotalCount);
            Assert.Equal(2, list.Count);
            Assert.Equal(3, list.TotalPages);
            Assert.Collection(list,
                i => Assert.Equal(1, i),
                i => Assert.Equal(2, i));
        }

        [Fact]
        public async Task CreatesPaginatedListFromEmptyQueryable()
        {
            var queryable = new TestAsyncEnumerable<int>(Enumerable.Empty<int>()).AsQueryable();

            var list = await PaginatedList.CreateAsync(queryable, 1, 2);

            Assert.Equal(0, list.TotalCount);
            Assert.Equal(0, list.Count);
            Assert.Equal(0, list.TotalPages);
            Assert.Empty(list);
        }

        public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IEnumerable<T> enumerable)
        {
            foreach (var item in enumerable)
            {
                yield return await Task.FromResult(item);
            }
        }
    }
}
