using Serious;
using Xunit;

public class EnumerableExtensionsTests
{
    record ItemIdentifier(string Identifier);

    public class TheTailMethod
    {
        [Fact]
        public void ReturnsEmptySequenceWhenEmpty()
        {
            var sequence = Array.Empty<string>();

            var (headList, tailItem) = sequence.Tail();

            Assert.Empty(headList);
            Assert.Null(tailItem);
        }

        [Fact]
        public void ReturnsEmptySequenceAndTailItemWhenSingleItem()
        {
            var sequence = new[] { "0" };

            var (headList, tailItem) = sequence.Tail();

            Assert.Empty(headList);
            Assert.Equal("0", tailItem);
        }

        [Fact]
        public void ReturnsAllItemsExceptTailAndTail()
        {
            var sequence = new[] { "0", "1", "2", "3" };

            var (headList, tailItem) = sequence.Tail();

            Assert.Equal(headList.ToArray(), new[] { "0", "1", "2" });
            Assert.Equal("3", tailItem);
        }
    }

    public class TheSelectWithPreviousMethod
    {
        [Fact]
        public void PassesResultOfPreviousValueToNext()
        {
            var sequence = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

            var result = sequence.SelectWithPrevious((num, prev) => num + prev, 0)
                .ToArray();

            Assert.Equal(new[] { 1, 3, 6, 10, 15, 21, 28, 36, 45, 55 }, result);
        }

        [Fact]
        public async Task PassesResultOfPreviousAsyncValueToNext()
        {
            var sequence = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

            var result = await sequence.SelectWithPrevious((num, prev) => Task.FromResult(num + prev), 0)
                .ToListAsync();

            Assert.Equal(new[] { 1, 3, 6, 10, 15, 21, 28, 36, 45, 55 }, result.ToArray());
        }
    }
    public class TheWhereFuzzyMatchMethod
    {
        [Fact]
        public void ReturnsElementsThatMatchTheFuzzyPredicate()
        {
            var lookup = new Dictionary<string, string>
            {
                ["address"] = "123 elm",
                ["phil's address"] = "321 elm",
                ["paul's address"] = "1243 elm",
                ["not-a-match"] = "blah blah"
            };

            var matches = lookup
                .WhereFuzzyMatch(kvp => kvp.Key, "addr")
                .ToList();

            Assert.Equal(3, matches.Count);
            Assert.Equal("address", matches[0].Key);
            Assert.Equal("phil's address", matches[1].Key);
            Assert.Equal("paul's address", matches[2].Key);
        }

        [Fact]
        public void ReturnsElementsThatMatchOneOrOtherPredicatePredicate()
        {
            var lookup = new Dictionary<string, string>
            {
                ["rem"] = "Remember things. Associate text and urls to a keyword or phrase.",
                ["wolfram"] = "An example integration with Wolfram Alpha's Short Answers API.",
                ["grafana"] = "Query grafana dashboards and generate visual graphs.",
                ["graph"] = "A shortcut to grafana",
                ["something"] = "Test your graphs",
                ["application-alerts"] = "An Http Trigger for responding to Application Insights alerts.",
                ["cloud-event"] = "Subscribe to Cloud Events webhook subscriptions (such as from Azure EventGrid) and report them to a chat room.",
                ["form-handler"] = "Testing out form handling via Python trigger.",
                ["warmup-ping"] = "Skill used to handle warmup pings when swapping slots for an App Service. We set an endpoint in `WEBSITE_SWAP_WARMUP_PING_PATH` that calls this skill via an HTTP trigger.",
                ["who"] = "Builds the story of a person by accumulating bits of fun information about that person. Also used to list who has permissions to a skill."
            };

            var matches = lookup
                .WhereFuzzyMatch(kvp => kvp.Key, kvp => kvp.Value, "graph")
                .ToList();

            Assert.Equal(3, matches.Count);
            Assert.Equal("grafana", matches[0].Key);
            Assert.Equal("graph", matches[1].Key);
            Assert.Equal("something", matches[2].Key);
        }
    }

    public class TheGetLineageMethod
    {
        [Fact]
        public void OrdersElementsByParentToChild()
        {
            var items = new[]
            {
                new SortItem { Id = 1, ParentId = 3 },
                new SortItem { Id = 4, ParentId = 2 },
                new SortItem { Id = 3, ParentId = null },
                new SortItem { Id = 2, ParentId = 1 }
            };

            var sorted = items.GetLineage(
                item => item.Id,
                item => item.ParentId,
                new SortItem { Id = 3, ParentId = null })
                .ToList();

            Assert.Equal(3, sorted[0].Id);
            Assert.Equal(1, sorted[1].Id);
            Assert.Equal(2, sorted[2].Id);
            Assert.Equal(4, sorted[3].Id);
        }

        [Fact]
        public void RetrievesTheSpecifiedLineageBasedOnRoot()
        {
            var items = new[]
            {
                new SortItem { Id = 1, ParentId = 3 },
                new SortItem { Id = 5, ParentId = 1 },
                new SortItem { Id = 4, ParentId = 2 },
                new SortItem { Id = 6, ParentId = 5 },
                new SortItem { Id = 3, ParentId = null },
                new SortItem { Id = 2, ParentId = null }
            };

            var sorted = items.GetLineage(
                    item => item.Id,
                    item => item.ParentId,
                    new SortItem { Id = 3, ParentId = null })
                .ToList();

            Assert.Equal(3, sorted[0].Id);
            Assert.Equal(1, sorted[1].Id);
            Assert.Equal(5, sorted[2].Id);
            Assert.Equal(6, sorted[3].Id);
        }

        [Fact]
        public void RetrievesLineageFromMiddle()
        {
            var items = new[]
            {
                new SortItem { Id = 1, ParentId = 3 },
                new SortItem { Id = 5, ParentId = 1 },
                new SortItem { Id = 4, ParentId = 2 },
                new SortItem { Id = 6, ParentId = 5 },
                new SortItem { Id = 3, ParentId = null },
                new SortItem { Id = 2, ParentId = null }
            };

            var sorted = items.GetLineage(
                    item => item.Id,
                    item => item.ParentId,
                    new SortItem { Id = 1, ParentId = 3 })
                .ToList();

            Assert.Equal(1, sorted[0].Id);
            Assert.Equal(5, sorted[1].Id);
            Assert.Equal(6, sorted[2].Id);
        }

        [Fact]
        public void EndsLineageAtCycle()
        {
            var items = new[]
            {
                new SortItem { Id = 1, ParentId = 3 },
                new SortItem { Id = 5, ParentId = 1 },
                new SortItem { Id = 6, ParentId = 5 },
                new SortItem { Id = 3, ParentId = 6 },
            };

            var sorted = items.GetLineage(
                    item => item.Id,
                    item => item.ParentId,
                    new SortItem { Id = 3, ParentId = null })
                .ToList();

            Assert.Equal(3, sorted[0].Id);
            Assert.Equal(1, sorted[1].Id);
            Assert.Equal(5, sorted[2].Id);
            Assert.Equal(6, sorted[3].Id);
        }
    }

    public class TheOrderByParentMethodWithRootPassedIn
    {
        [Fact]
        public void RetrievesEveryLineage()
        {
            var items = new[]
            {
                new SortItem { Id = 7, ParentId = 4 },
                new SortItem { Id = 1, ParentId = 3 },
                new SortItem { Id = 5, ParentId = 1 },
                new SortItem { Id = 4, ParentId = 2 },
                new SortItem { Id = 6, ParentId = 5 },
                new SortItem { Id = 3, ParentId = null },
                new SortItem { Id = 2, ParentId = null }
            };

            var sorted = items.GetLineages(
                    item => item.Id,
                    item => item.ParentId)
                .ToList();

            Assert.Equal(2, sorted.Count);
            var firstLineage = sorted[0].ToList();
            Assert.Equal(3, firstLineage[0].Id);
            Assert.Equal(1, firstLineage[1].Id);
            Assert.Equal(5, firstLineage[2].Id);
            Assert.Equal(6, firstLineage[3].Id);
            var secondLineage = sorted[1].ToList();
            Assert.Equal(2, secondLineage[0].Id);
            Assert.Equal(4, secondLineage[1].Id);
            Assert.Equal(7, secondLineage[2].Id);

        }
    }

    public class SortItem
    {
        public int Id { get; set; }
        public int? ParentId { get; set; }
    }

    public class TheFlattenMethod
    {
        public class Element
        {
            public int Id { get; set; }
        }

        public class ElementWithChildren : Element
        {
            public IEnumerable<Element> Children { get; init; } = Array.Empty<Element>();
        }

        [Fact]
        public void RetrievesEveryElementOfFlatSequence()
        {
            var numbers = new[] { 0, 1, 2, 3 };

            var flattened = numbers.Flatten(_ => Enumerable.Empty<int>());

            Assert.Equal(numbers, flattened);
        }

        [Fact]
        public void RetrievesEveryElementOfHierarchy()
        {
            var hierarchy = new Element[]
            {
                new() {Id = 1},
                new ElementWithChildren
                {
                    Id = 2,
                    Children = new Element[]
                    {
                        new() {Id = 3},
                        new() {Id = 4},
                        new ElementWithChildren
                        {
                            Id = 5,
                            Children = new Element[]
                            {
                                new() {Id = 6},
                                new() {Id = 7},
                            }
                        }
                    }
                },
                new() {Id = 8},
            };

            var flattened = hierarchy.Flatten(e => e is ElementWithChildren elementWithChildren
                ? elementWithChildren.Children
                : Enumerable.Empty<Element>())
                .ToList();

            Assert.Equal(8, flattened.Count);
            for (int i = 0; i < flattened.Count; i++)
            {
                Assert.Equal(i + 1, flattened[i].Id);
            }
        }
    }

    public class TheEnsureGroupsMethod
    {
        record R(string Key, int Value);

        [Fact]
        public void ReturnsEmptyGroupForMissingGroups()
        {
            var elements = new[]
            {
                new R("A", 1),
                new R("A", 2),
                new R("C", 3),
                new R("C", 4),
                new R("C", 5),
                new R("D", 6)
            };
            var groups = elements.GroupBy(e => e.Key);

            var results = groups.EnsureGroups(new[] { "A", "B", "C", "D", "E" })
                .OrderBy(g => g.Key);


            Assert.Collection(results,
                g0 => AssertGroup(("A", new[] { 1, 2 }), g0),
                g1 => AssertGroup(("B", Array.Empty<int>()), g1),
                g2 => AssertGroup(("C", new[] { 3, 4, 5 }), g2),
                g3 => AssertGroup(("D", new[] { 6 }), g3),
                g4 => AssertGroup(("E", Array.Empty<int>()), g4));
        }

        static void AssertGroup((string, int[]) expected, IGrouping<string, R> group)
        {
            var (expectedKey, expectedValues) = expected;
            Assert.Equal(expectedKey, group.Key);
            Assert.Equal(expectedValues, group.Select(e => e.Value).ToArray());
        }
    }

    public class TheAggregateGroupsMethod
    {
        record struct R(string Date, int Opened, int Closed);

        [Fact]
        public void GroupsItemsWithGroupAggregators()
        {
            var data = new[]
            {
                new R(Date: "2020-04-19", Opened: 1, Closed: 1), // carry[-1] = 1. Sum(Add) = 3, Sum(Subtract) = 1
                new R(Date: "2020-04-19", Opened: 2, Closed: 0), // result[0] = carry[-1] + Sum(add) = *4*. carry[0] = 3.

                new R(Date: "2020-04-21", Opened: 3, Closed: 2), // carry[0] = 2. Sum(Add) = 12, Sum(Subtract) = 13.
                new R(Date: "2020-04-21", Opened: 4, Closed: 1),
                new R(Date: "2020-04-21", Opened: 5, Closed: 10),// result[1] = carry[0] + Sum(add) = *15*. carry[1] = 1.

                new R(Date: "2020-04-22", Opened: 2, Closed: 1)  // carry[1] = 1. Sum(Add) = 2, result[2] = *3*
            };
            var days = new[] { "2020-04-19", "2020-04-20", "2020-04-21", "2020-04-22" };
            var groups = data
                .GroupBy(r => r.Date)
                .EnsureGroups(days)
                .OrderBy(g => g.Key);

            var results = groups.AggregateGroups(
                (carry, g) => carry + g.Sum(r => r.Opened),
                (currentValue, g) => currentValue - g.Sum(r => r.Closed),
                seed: 1);

            Assert.Collection(results,
                b0 => Assert.Equal(("2020-04-19", 4), (b0.Key, b0.Value)),
                b1 => Assert.Equal(("2020-04-20", 3), (b1.Key, b1.Value)),
                b2 => Assert.Equal(("2020-04-21", 15), (b2.Key, b2.Value)),
                b3 => Assert.Equal(("2020-04-22", 4), (b3.Key, b3.Value)));
        }

        [Fact]
        public void GroupsItemsWithGroupCountAggregators()
        {
            var data = new[]
            {
                new R(Date: "2020-04-19", Opened: 1, Closed: 1), // carry[-1] = 1. Sum(Add) = 3, Sum(Subtract) = 1
                new R(Date: "2020-04-19", Opened: 2, Closed: 0), // result[0] = carry[-1] + Sum(add) = *4*. carry[0] = 3.

                new R(Date: "2020-04-21", Opened: 3, Closed: 2), // carry[0] = 2. Sum(Add) = 12, Sum(Subtract) = 13.
                new R(Date: "2020-04-21", Opened: 4, Closed: 1),
                new R(Date: "2020-04-21", Opened: 5, Closed: 10),// result[1] = carry[0] + Sum(add) = *15*. carry[1] = 1.

                new R(Date: "2020-04-22", Opened: 2, Closed: 1)  // carry[1] = 1. Sum(Add) = 2, result[2] = *3*
            };
            var days = new[] { "2020-04-19", "2020-04-20", "2020-04-21", "2020-04-22" };
            var groups = data
                .GroupBy(r => r.Date)
                .EnsureGroups(days)
                .OrderBy(g => g.Key);

            var results = groups.AggregateGroups(
                g => g.Sum(r => r.Opened),
                g => -1 * g.Sum(r => r.Closed),
                seed: 1);

            Assert.Collection(results,
                b0 => Assert.Equal(("2020-04-19", 4), (b0.Key, b0.Value)),
                b1 => Assert.Equal(("2020-04-20", 3), (b1.Key, b1.Value)),
                b2 => Assert.Equal(("2020-04-21", 15), (b2.Key, b2.Value)),
                b3 => Assert.Equal(("2020-04-22", 4), (b3.Key, b3.Value)));
        }
    }

    public class TheSyncMethod
    {
        record SyncItem(string Id, string Name);


        [Fact]
        public void ModifiesCollectionToFinalState()
        {
            var source = new List<SyncItem> { new("Id1", "Foo"), new("Id2", "Bar"), new("Id3", "Baz") };
            var target = new List<ItemIdentifier> { new("Id2"), new("Id4") };
            var added = new List<SyncItem>();
            var removed = new List<SyncItem>();

            var modified = source.Sync(
                target,
                (sourceItem, targetItem) => sourceItem.Id == targetItem.Identifier,
                targetItem => new SyncItem(targetItem.Identifier, $"Item{targetItem.Identifier}"),
                (collection, itemToAdd) => {
                    added.Add(itemToAdd);
                    collection.Add(itemToAdd);
                },
                (collection, itemToRemove) => {
                    removed.Add(itemToRemove);
                    collection.Remove(itemToRemove);
                });

            Assert.True(modified);
            Assert.Equal("Id4", Assert.Single(added).Id);
            Assert.Collection(removed,
                r0 => Assert.Equal("Id1", r0.Id),
                r1 => Assert.Equal("Id3", r1.Id));
            Assert.Collection(source,
                i0 => Assert.Equal("Id2", i0.Id),
                i1 => Assert.Equal("Id4", i1.Id));
        }

        [Fact]
        public void WithDefaultAddAndRemoveMethodsModifiesCollectionToFinalState()
        {
            var source = new List<SyncItem> { new("Id1", "Foo"), new("Id2", "Bar"), new("Id3", "Baz") };
            var targetKeys = new List<string> { "Id2", "Id4" };

            var modified = source.Sync(
                targetKeys,
                (sourceItem, targetItem) => sourceItem.Id == targetItem,
                key => new SyncItem(key, $"Item{key}"));

            Assert.True(modified);
            Assert.Collection(source,
                i0 => Assert.Equal("Id2", i0.Id),
                i1 => Assert.Equal("Id4", i1.Id));
        }

        [Fact]
        public void WithSameTypeForSourceAndTargetAlsoSyncsCorrectly()
        {
            var source = new List<ItemIdentifier> { new("Id1"), new("Id2"), new("Id3") };
            var target = new List<ItemIdentifier> { new("Id2"), new("Id4") };
            var added = new List<ItemIdentifier>();
            var removed = new List<ItemIdentifier>();

            var modified = source.Sync(
                target,
                item => item.Identifier,
                targetItem => new ItemIdentifier(targetItem.Identifier),
                (collection, itemToAdd) => {
                    added.Add(itemToAdd);
                    collection.Add(itemToAdd);
                },
                (collection, itemToRemove) => {
                    removed.Add(itemToRemove);
                    collection.Remove(itemToRemove);
                });

            Assert.True(modified);
            Assert.Equal("Id4", Assert.Single(added).Identifier);
            Assert.Collection(removed,
                r0 => Assert.Equal("Id1", r0.Identifier),
                r1 => Assert.Equal("Id3", r1.Identifier));
            Assert.Collection(source,
                i0 => Assert.Equal("Id2", i0.Identifier),
                i1 => Assert.Equal("Id4", i1.Identifier));
        }

        [Fact]
        public void WithSameTypeForSourceAndTargetAndNoLookupNeededAlsoSyncsCorrectly()
        {
            var source = new List<ItemIdentifier> { new("Id1"), new("Id2"), new("Id3") };
            var target = new List<ItemIdentifier> { new("Id2"), new("Id4") };
            var added = new List<ItemIdentifier>();
            var removed = new List<ItemIdentifier>();

            var modified = source.Sync(
                target,
                item => item.Identifier,
                (collection, itemToAdd) => {
                    added.Add(itemToAdd);
                    collection.Add(itemToAdd);
                },
                (collection, itemToRemove) => {
                    removed.Add(itemToRemove);
                    collection.Remove(itemToRemove);
                });

            Assert.True(modified);
            Assert.Equal("Id4", Assert.Single(added).Identifier);
            Assert.Collection(removed,
                r0 => Assert.Equal("Id1", r0.Identifier),
                r1 => Assert.Equal("Id3", r1.Identifier));
            Assert.Collection(source,
                i0 => Assert.Equal("Id2", i0.Identifier),
                i1 => Assert.Equal("Id4", i1.Identifier));
        }
    }

    public class TheDelimitMethod
    {
        [Fact]
        public void ReturnsEmptySequence()
        {
            var source = Enumerable.Empty<ItemIdentifier>();

            var delimited = source.Delimit(new("_")).ToArray();

            Assert.Empty(delimited);
        }

        [Fact]
        public void ReturnsSingleItemSequence()
        {
            var source = new ItemIdentifier[] { new("Id1"), };

            var delimited = source.Delimit(new("_")).ToArray();

            Assert.Equal(new ItemIdentifier[] { new("Id1") }, delimited);
        }

        [Fact]
        public void ReturnsDelimitedSequence()
        {
            var source = new ItemIdentifier[]
            {
                new("Id1"),
                new("Id2"),
                new("Id3")
            };

            var delimited = source.Delimit(new("_")).ToArray();

            Assert.Equal(new ItemIdentifier[]
            {
                new("Id1"),
                new("_"),
                new("Id2"),
                new("_"),
                new("Id3")
            }, delimited);
        }
    }

    public class TheSelectWithNextMethod
    {
        [Fact]
        public void ReturnsEmptyForEmptySequence()
        {
            var source = Enumerable.Empty<ItemIdentifier>();

            var result = source.SelectWithNext();

            Assert.Empty(result);
        }

        [Fact]
        public void ReturnsSingleForSingleSequence()
        {
            var source = new[] { new ItemIdentifier("1") };

            var result = source.SelectWithNext();

            var single = Assert.Single(result);
            Assert.Equal((new ItemIdentifier("1"), null), single);
        }
    }
}
