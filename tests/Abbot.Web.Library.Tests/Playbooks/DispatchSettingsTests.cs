using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Serious.Abbot.Forms;
using Serious.Abbot.Playbooks;
using Serious.TestHelpers.CultureAware;

public class DispatchSettingsTests
{
    public class TheEqualsMethod
    {
        [Fact]
        public void Works()
        {
            var customers = new[]
            {
                DispatchSettings.Default,
                new DispatchSettings { Type = DispatchType.Once },
                ByCustomer(),
                ByCustomer(),
                ByCustomer("A"),
                ByCustomer("A"),
                ByCustomer("A", "B"),
                ByCustomer("A", "B", "A"), // Ignore duplicates
                ByCustomer("B", "A"),
                ByCustomer("B"),
                ByCustomer("A", "B", "C"),
                ByCustomer("B", "C"),
                ByCustomer("C"),
            };

            var expectedToBeEqual = new HashSet<(int, int)>
            {
                (0, 1),
                (2, 3),
                (4, 5),
                (6, 7),
                (6, 8),
                (7, 8),
            };

            for (int i = 0; i < customers.Length; i++)
            {
                for (int j = i + 1; j < customers.Length; j++)
                {
                    if (expectedToBeEqual.Contains((i, j)))
                    {
                        AssertEquality(customers[i], customers[j]);
                    }
                    else
                    {
                        AssertNotEquality(customers[i], customers[j]);
                    }
                }
            }
        }

        DispatchSettings ByCustomer(params string[] segments) =>
            new DispatchSettings { Type = DispatchType.ByCustomer, CustomerSegments = segments };

        void AssertEquality(DispatchSettings left, DispatchSettings right)
        {
            Assert.Equal(left, right);
            Assert.Equal(right, left);
            Assert.Equal(left.GetHashCode(), right.GetHashCode());
        }

        void AssertNotEquality(DispatchSettings left, DispatchSettings right)
        {
            Assert.NotEqual(left, right);
            Assert.NotEqual(right, left);
        }
    }

    public class TheToStringMethod
    {
        [Fact]
        public void DefaultIsOnce()
        {
            var settings = DispatchSettings.Default;

            Assert.Equal("Once", settings.ToString());
        }

        [Fact]
        public void OnceIsOnce()
        {
            var settings = new DispatchSettings { Type = DispatchType.Once };

            Assert.Equal("Once", settings.ToString());
        }

        [Fact]
        public void ByCustomerWithoutSegmentsIsEachCustomer()
        {
            var settings = new DispatchSettings { Type = DispatchType.ByCustomer };

            Assert.Equal("Each Customer", settings.ToString());
        }

        [UseCulture("en-US")]
        [Theory]
        [InlineData("Each Customer in Awesome", "Awesome")]
        [InlineData("Each Customer in Awesome or Great", "Awesome", "Great")]
        [InlineData("Each Customer in Awesome, Great, or Terrific", "Awesome", "Great", "Terrific")]
        public void ByCustomerWithSegmentsIsEachCustomerWithSegments(string expected, params string[] segments)
        {
            var settings = new DispatchSettings { Type = DispatchType.ByCustomer, CustomerSegments = segments };

            Assert.Equal(expected, settings.ToString());
        }
    }
}
