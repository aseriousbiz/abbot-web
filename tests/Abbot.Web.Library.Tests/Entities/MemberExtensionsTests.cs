using System.Globalization;
using Serious.Abbot.Entities;
using Serious.Abbot.Security;

public class MemberExtensionsTests
{
    public class TheIsInRoleByIdMethodMethod
    {
        [Fact]
        public void IsTrueWhenActiveInRole()
        {
            var member = new Member
            {
                Active = true,
                MemberRoles = new List<MemberRole>
                {
                    new()
                    {
                        Role = new Role
                        {
                            Id = 42,
                            Name = Roles.Agent
                        }
                    },
                    new()
                    {
                        Role = new Role
                        {
                            Id = 23,
                            Name = Roles.Administrator
                        }
                    }
                }
            };
            Assert.False(member.IsInRoleByRoleId(42));
        }

        [Fact]
        public void IsFalseWhenActiveButNotInRole()
        {
            var member = new Member
            {
                Active = true,
                MemberRoles = new List<MemberRole>
                {
                    new()
                    {
                        Role = new Role
                        {
                            Id = 42,
                            Name = Roles.Agent
                        }
                    },
                    new()
                    {
                        Role = new Role
                        {
                            Id = 23,
                            Name = Roles.Administrator
                        }
                    }
                }
            };
            Assert.False(member.IsInRoleByRoleId(19));
        }

        [Fact]
        public void IsFalseWhenNotActiveInRole()
        {
            var member = new Member
            {
                Active = false,
                MemberRoles = new List<MemberRole>
                {
                    new()
                    {
                        Role = new Role
                        {
                            Id = 42,
                            Name = Roles.Administrator
                        }
                    }
                }
            };
            Assert.False(member.IsInRoleByRoleId(42));
        }
    }

    public class TheIsInRoleMethodMethod
    {
        [Fact]
        public void IsTrueWhenActiveInRole()
        {
            var member = new Member
            {
                Active = true,
                MemberRoles = new List<MemberRole>
                {
                    new()
                    {
                        Role = new Role
                        {
                            Name = Roles.Agent
                        }
                    }
                }
            };
            Assert.True(member.IsInRole(Roles.Agent));
        }

        [Fact]
        public void IsFalseWhenActiveButNotInRole()
        {
            var member = new Member
            {
                Active = true,
                MemberRoles = new List<MemberRole>
                {
                    new()
                    {
                        Role = new Role
                        {
                            Name = Roles.Administrator
                        }
                    }
                }
            };
            Assert.False(member.IsInRole(Roles.Agent));
        }

        [Fact]
        public void IsFalseWhenActiveInRole()
        {
            var member = new Member
            {
                Active = false,
                MemberRoles = new List<MemberRole>
                {
                    new()
                    {
                        Role = new Role
                        {
                            Name = Roles.Agent
                        }
                    }
                }
            };
            Assert.False(member.IsInRole(Roles.Agent));
        }
    }

    public class TheIsAdministratorMethod
    {
        [Fact]
        public void IsTrueWhenActiveInRole()
        {
            var member = new Member
            {
                Active = true,
                MemberRoles = new List<MemberRole>
                {
                    new()
                    {
                        Role = new Role
                        {
                            Name = Roles.Administrator
                        }
                    }
                }
            };
            Assert.True(member.IsAdministrator());
        }

        [Fact]
        public void IsFalseWhenActiveNotInRole()
        {
            var member = new Member
            {
                Active = true,
                MemberRoles = new List<MemberRole>
                {
                    new()
                    {
                        Role = new Role
                        {
                            Name = Roles.Agent
                        }
                    }
                }
            };
            Assert.False(member.IsAdministrator());
        }

        [Fact]
        public void IsFalseWhenActiveInRole()
        {
            var member = new Member
            {
                Active = false,
                MemberRoles = new List<MemberRole>
                {
                    new()
                    {
                        Role = new Role
                        {
                            Name = Roles.Administrator
                        }
                    }
                }
            };
            Assert.False(member.IsAdministrator());
        }
    }

    public class TheIsStaffMethod
    {
        [Fact]
        public void IsTrueWhenActiveInRole()
        {
            var member = new Member
            {
                Active = true,
                MemberRoles = new List<MemberRole>
                {
                    new()
                    {
                        Role = new Role
                        {
                            Name = Roles.Staff
                        }
                    }
                }
            };
            Assert.True(member.IsStaff());
        }

        [Fact]
        public void IsFalseWhenActiveNotInRole()
        {
            var member = new Member
            {
                Active = true,
                MemberRoles = new List<MemberRole>
                {
                    new()
                    {
                        Role = new Role
                        {
                            Name = Roles.Administrator
                        }
                    }
                }
            };
            Assert.False(member.IsStaff());
        }

        [Fact]
        public void IsFalseWhenActiveInRole()
        {
            var member = new Member
            {
                Active = false,
                MemberRoles = new List<MemberRole>
                {
                    new()
                    {
                        Role = new Role
                        {
                            Name = Roles.Staff
                        }
                    }
                }
            };
            Assert.False(member.IsStaff());
        }
    }

    public class TheIsAbbotMethod
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void IsTrueIfIsAbbot(bool isAbbot)
        {
            var member = new Member
            {
                User = new User
                {
                    PlatformUserId = "Doesn't matter",
                    NameIdentifier = "Also doesn't matter",
                    IsBot = false, // Again, doesn't matter
                    IsAbbot = isAbbot,
                },
            };
            Assert.Equal(isAbbot, member.IsAbbot());
        }
    }

    public class TheGetNextWorkingHoursStartDateUtcMethod
    {
        [Theory]
        [InlineData("2023-08-11T22:30:00Z", "America/Los_Angeles", "2023-08-14T16:00:00Z")]
        [InlineData("2023-08-09T22:30:00Z", "America/Los_Angeles", "2023-08-10T16:00:00Z")]
        [InlineData("2023-08-12T00:00:00Z", "UTC", "2023-08-14T09:00:00Z")]
        public void ReturnsNextWorkingHoursStartDateUtc(
            string nowUtcString,
            string timeZoneId,
            string expected)
        {
            var nowUtc = DateTime.Parse(
                nowUtcString,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal);
            var member = new Member
            {
                TimeZoneId = timeZoneId,
                WorkingHours = new WorkingHours(new TimeOnly(9, 0), new TimeOnly(15, 0)),
                Properties = new MemberProperties(
                    new NotificationSettings(true, false),
                    new WorkingDays(
                        Monday: true,
                        Tuesday: true,
                        Wednesday: true,
                        Thursday: true,
                        Friday: true,
                        Saturday: false,
                        Sunday: false)),
            };

            var nextWorkingHoursStartDateUtc = member.GetNextWorkingHoursStartDateUtc(nowUtc);

            Assert.Equal(
                expected,
                nextWorkingHoursStartDateUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
        }

        [Fact]
        public void ReturnsNextDayWhenNoWorkingDays()
        {
            var nowUtc = DateTime.Parse(
                "2023-08-11T22:30:00Z",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal);
            var member = new Member
            {
                TimeZoneId = "UTC",
                WorkingHours = new WorkingHours(new TimeOnly(9, 0), new TimeOnly(15, 0)),
                Properties = new MemberProperties(
                    new NotificationSettings(true, false),
                    new WorkingDays(
                        Monday: false,
                        Tuesday: false,
                        Wednesday: false,
                        Thursday: false,
                        Friday: false,
                        Saturday: false,
                        Sunday: false)),
            };

            var nextWorkingHoursStartDateUtc = member.GetNextWorkingHoursStartDateUtc(nowUtc);

            Assert.Equal(
                "2023-08-12T09:00:00Z",
                nextWorkingHoursStartDateUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
        }
    }
}
