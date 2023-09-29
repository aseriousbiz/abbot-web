using System.Globalization;
using System.Security.Claims;
using Abbot.Common.TestHelpers;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Security;
using Serious.Slack;
using Serious.Slack.Events;
using Serious.TestHelpers;

public class UserRepositoryTests
{
    public class TheGetUserByPlatformUserIdMethod
    {
        [Fact]
        public async Task ReturnsNullWhenNoUserMatches()
        {
            var env = TestEnvironment.Create();
            var repo = env.Activate<UserRepository>();

            var user = await repo.GetUserByPlatformUserId(NonExistent.PlatformUserId);

            Assert.Null(user);
        }

        [Fact]
        public async Task ReturnsExistingSlackUserAndMember()
        {
            var env = TestEnvironment.Create();
            var existingMember = env.TestData.Member;
            var existingUser = existingMember.User;
            var repo = env.Activate<UserRepository>();

            var user = await repo.GetUserByPlatformUserId(existingUser.PlatformUserId);

            Assert.NotNull(user);
            var member = Assert.Single(user.Members);
            Assert.Equal(existingUser.Id, user.Id);
            Assert.Equal(existingMember.Id, member.Id);
        }

        [Fact]
        public async Task ReturnsNullForMissingSlackAbbotUser()
        {
            var env = TestEnvironment.Create();
            var foreignOrganization = env.TestData.ForeignOrganization;
            Assert.NotNull(foreignOrganization.PlatformBotUserId);
            var repo = env.Activate<UserRepository>();

            var user = await repo.GetUserByPlatformUserId(foreignOrganization.PlatformBotUserId);

            Assert.Null(user);
        }
    }

    public class TheGetMemberByIdAsyncMethod
    {
        [Fact]
        public async Task ReturnsMemberFromDatabase()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var expectedMember = env.TestData.Member;
            var repository = env.Activate<UserRepository>();

            var result = await repository.GetMemberByIdAsync(expectedMember, organization);

            Assert.NotNull(result);
            Assert.Equal(expectedMember.User.Email, result.User.Email);
            Assert.Equal(organization.PlatformId, result.Organization.PlatformId);
        }
    }

    public class TheGetMemberByEmailAsyncMethod
    {
        [Fact]
        public async Task ReturnsNullOnNoMatch()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var repository = env.Activate<UserRepository>();

            var result = await repository.GetMemberByEmailAsync(organization, "hoop@dedoop.example.com");

            Assert.Null(result);
        }

        [Fact]
        public async Task ReturnsSingleMatchingMemberFromTheDatabase()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var expectedMember = env.TestData.Member;
            var repository = env.Activate<UserRepository>();

            var result = await repository.GetMemberByEmailAsync(organization, env.TestData.User.Email!);

            Assert.NotNull(result);
            Assert.Equal(expectedMember.Id, result.Id);
            Assert.Equal(organization.PlatformId, result.Organization.PlatformId);
        }

        [Fact]
        public async Task IgnoresMemberWhereSlackTeamIdMismatches()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var expectedMember = env.TestData.Member;
            var repository = env.Activate<UserRepository>();
            var potentialDuplicate = await env.CreateMemberAsync(email: env.TestData.User.Email!);
            potentialDuplicate.User.SlackTeamId = "T123";
            await env.Db.SaveChangesAsync();

            var result = await repository.GetMemberByEmailAsync(organization, env.TestData.User.Email!);

            Assert.NotNull(result);
            Assert.Equal(expectedMember.Id, result.Id);
            Assert.Equal(organization.PlatformId, result.Organization.PlatformId);
        }

        [Fact]
        public async Task ReturnsNullOnDuplicateUser()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var expectedMember = env.TestData.Member;
            var repository = env.Activate<UserRepository>();
            await env.CreateMemberAsync(email: expectedMember.User.Email!);

            var result = await repository.GetMemberByEmailAsync(organization, env.TestData.User.Email!);

            Assert.Null(result);
        }
    }

    public class TheGetCurrentMemberAsyncMethod
    {
        [Fact]
        public async Task ReturnsCurrentMemberFromDatabase()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var user = env.TestData.User;
            var role = new Role { Name = "PeanutGallery", Description = "Peanut Gallery" };
            env.TestData.Member.MemberRoles.Add(new MemberRole { Role = role });
            await env.Db.Roles.AddAsync(role);
            await env.Db.SaveChangesAsync();
            env.Db.Entry(env.TestData.Member).State = EntityState.Detached;

            var principal = new ClaimsPrincipal(new ClaimsIdentity(
                new[]
                {
                    new Claim($"{AbbotSchema.SchemaUri}platform_user_id", user.PlatformUserId),
                    new Claim(ClaimTypes.NameIdentifier, $"oauth2|fake|{user.PlatformUserId}"),
                    new Claim($"{AbbotSchema.SchemaUri}platform_id", organization.PlatformId)
                }));
            var repository = env.Activate<UserRepository>();

            var result = await repository.GetCurrentMemberAsync(principal);

            Assert.NotNull(result);
            Assert.Equal(EntityState.Unchanged, env.Db.Entry(result).State);
            Assert.NotSame(env.TestData.Member, result);
            Assert.Equal(organization.PlatformId, result.Organization.PlatformId);
            Assert.Equal("PeanutGallery", result.MemberRoles.Single().Role.Name);
        }

        [Fact]
        public async Task ReturnsOnlyOneCurrentMemberFromDatabase()
        {
            var env = TestEnvironment.Create();
            var user = env.TestData.User;
            var otherOrg = await env.CreateOrganizationAsync(platformId: "T009090909");
            var role = new Role { Name = "PeanutGallery", Description = "Peanut Gallery" };
            var role2 = new Role { Name = "Plebs", Description = "Plebs" };
            await env.Db.Roles.AddRangeAsync(role, role2);
            otherOrg.Members.Add(new Member
            {
                User = user,
                MemberRoles = new List<MemberRole> { new() { Role = role2 } }
            });
            await env.Db.SaveChangesAsync();
            var principal = new ClaimsPrincipal(new ClaimsIdentity(
                new[]
                {
                    new Claim($"{AbbotSchema.SchemaUri}platform_user_id", user.PlatformUserId),
                    new Claim(ClaimTypes.NameIdentifier, $"oauth2|fake|{user.PlatformUserId}"),
                    new Claim($"{AbbotSchema.SchemaUri}platform_id", "T009090909")
                }));
            var repository = env.Activate<UserRepository>();

            var result = await repository.GetCurrentMemberAsync(principal);

            Assert.NotNull(result);
            Assert.Equal("T009090909", result.Organization.PlatformId);
            Assert.Equal("Plebs", result.MemberRoles.Single().Role.Name);
        }
    }

    public class TheEnsureCurrentMemberWithRolesAsyncMethod
    {
        [Fact]
        public async Task ReturnsUserThatMatchesPlatformUserIdAndOrganizationAndUpdatesSlackTeamId()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var user = env.TestData.User;
            user.DisplayName = "UserNameFromDatabase";
            user.NameIdentifier = "oauth2|slack|U0123";
            user.Email = "test@example.com";
            var principal = new FakeClaimsPrincipal(
                organization.PlatformId,
                user.PlatformUserId,
                email: "test@example.com",
                nameIdentifier: user.NameIdentifier,
                name: "UserNameFromClaims");

            var repository = env.Activate<UserRepository>();
            var result = await repository.EnsureCurrentMemberWithRolesAsync(principal, organization);

            Assert.NotNull(result);
            Assert.True(result.Active);
            Assert.Equal("UserNameFromDatabase", result.User.DisplayName); // Not updated
            Assert.Equal("test@example.com", result.User.Email);
            Assert.Equal(user.PlatformUserId, result.User.PlatformUserId);
            Assert.Equal(organization.PlatformId, result.User.SlackTeamId);
            Assert.Equal(organization.PlatformId, result.Organization.PlatformId);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("RealNameFromDatabase")]
        public async Task UpdatesExistingUserThatMatchesPlatformUserIdAndOrganizationAndDoesNotCreateNewOne(string? realName)
        {
            var env = TestEnvironment.Create();
            var user = env.TestData.User;
            user.DisplayName = "UserNameFromDatabase";
            user.RealName = realName;
            var organization = env.TestData.Organization;
            var platformUserId = user.PlatformUserId;
            var principal = new FakeClaimsPrincipal(
                name: "UserNameFromClaims",
                email: "tester@example.com",
                platformId: organization.PlatformId,
                platformUserId: platformUserId);
            var repository = env.Activate<UserRepository>();

            var result = await repository.EnsureCurrentMemberWithRolesAsync(principal, organization);

            Assert.NotNull(result);
            Assert.True(result.Active);
            var users = await env.Db.Users.Where(u => u.PlatformUserId == platformUserId).ToListAsync();
            Assert.Single(users);
            Assert.Empty(users.GroupBy(u => u.PlatformUserId).Select(g => g.Count()).Where(count => count > 1));
            Assert.Equal($"oauth|slack|{platformUserId}", result.User.NameIdentifier);
            Assert.Equal("UserNameFromDatabase", result.User.DisplayName); // Not updated
            Assert.Equal(realName, result.User.RealName); // Not updated
            Assert.Equal("tester@example.com", result.User.Email);
            Assert.Equal(platformUserId, result.User.PlatformUserId);
            Assert.Equal(organization.PlatformId, result.Organization.PlatformId);
        }

        [Fact]
        public async Task UpdatesExistingUserAvatarUserNameAndEmailIfDifferentAndNotNull()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var user = env.TestData.User;
            user.DisplayName = "UserNameFromDatabase";
            await env.Db.SaveChangesAsync();
            var principal = new FakeClaimsPrincipal(
                organization.PlatformId,
                user.PlatformUserId,
                avatar: "https://robohash.org?cool",
                email: "newemail@example.com",
                name: "UserNameFromClaims");
            var repository = env.Activate<UserRepository>();

            var result = await repository.EnsureCurrentMemberWithRolesAsync(principal, organization);

            Assert.NotNull(result);
            Assert.Equal("newemail@example.com", result.User.Email);
            Assert.Equal(user.PlatformUserId, result.User.PlatformUserId);
            Assert.Equal(organization.PlatformId, result.Organization.PlatformId);
            Assert.Equal("https://robohash.org?cool", result.User.Avatar);
            Assert.Equal("UserNameFromDatabase", result.User.DisplayName); // Not updated
        }

        [Fact]
        public async Task DoesNotOverwriteEmailWithNull()
        {
            var env = TestEnvironment.Create();
            var user = env.TestData.User;
            user.Email = "tester@example.com";
            await env.Db.SaveChangesAsync();
            var organization = env.TestData.Organization;
            var principal = new FakeClaimsPrincipal(
                organization.PlatformId,
                user.PlatformUserId,
                avatar: "https://example.com/mug.jpg",
                email: null);

            var repository = env.Activate<UserRepository>();

            var result = await repository.EnsureCurrentMemberWithRolesAsync(principal, organization);

            Assert.NotNull(result);
            Assert.Equal("tester@example.com", result.User.Email);
            Assert.Equal("https://example.com/mug.jpg", result.User.Avatar);
        }

        [Fact]
        public async Task ThrowsExceptionWhenPlatformUserIdClaimMissing()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var principal = new ClaimsPrincipal();
            var repository = env.Activate<UserRepository>();

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                repository.EnsureCurrentMemberWithRolesAsync(principal, organization));
        }

        [Theory]
        [InlineData(null, "unknown")]
        [InlineData("UserNameFromClaims", "UserNameFromClaims")]
        public async Task CreatesMissingUserFromPrincipal(string? name, string expectedDisplayName)
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            await env.Db.SaveChangesAsync();
            var principal = new FakeClaimsPrincipal(
                organization.PlatformId,
                "UF0",
                avatar: "https://robohash.org?cool",
                email: "newemail@example.com",
                name: name);
            var repository = env.Activate<UserRepository>();

            var result = await repository.EnsureCurrentMemberWithRolesAsync(principal, organization);

            Assert.NotNull(result);
            Assert.Equal("newemail@example.com", result.User.Email);
            Assert.Equal("UF0", result.User.PlatformUserId);
            Assert.Equal(organization.PlatformId, result.Organization.PlatformId);
            Assert.Equal("https://robohash.org?cool", result.User.Avatar);
            Assert.Equal(expectedDisplayName, result.User.DisplayName);
            Assert.Null(result.User.RealName);
        }
    }

    public class TheGetActiveUsersQueryableMethod
    {
        [Fact]
        public async Task RetrievesActiveUsers()
        {
            var env = TestEnvironment.Create();
            var user = env.TestData.User;
            var member = env.TestData.Member;
            member.MemberRoles.Add(new MemberRole
            {
                Role = new Role { Name = "Member", Description = "X" }
            });
            var organization = env.TestData.Organization;
            organization.Members.Add(new Member
            {
                User = new User
                {
                    DisplayName = "cook",
                    Email = "test2@example.com",
                    NameIdentifier = $"oauth2|slack|{user.PlatformUserId}",
                    PlatformUserId = user.PlatformUserId
                },
                Active = false,
                MemberRoles = new List<MemberRole>
                {
                    new() { Role = new Role { Name = "Member", Description = "X"} }
                }
            });
            await env.Db.SaveChangesAsync();
            var repository = env.Activate<UserRepository>();

            var archived = await repository.GetActiveMembersQueryable(organization).SingleAsync();

            Assert.Equal(user.PlatformUserId, archived.User.PlatformUserId);
        }

        [Fact]
        public async Task IgnoresUsersWithNoRole()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var repository = env.Activate<UserRepository>();

            var actual = await repository.GetActiveMembersQueryable(organization).ToListAsync();

            Assert.Empty(actual);
        }

        [Fact]
        public async Task IgnoresBotUsers()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var member = env.TestData.Member;
            member.User.IsBot = true;
            member.MemberRoles.Add(new MemberRole { Role = new Role { Name = "Member", Description = "X" } });
            await env.Db.SaveChangesAsync();
            var repository = env.Activate<UserRepository>();

            var archived = await repository.GetActiveMembersQueryable(organization).ToListAsync();

            Assert.Empty(archived);
        }

        [Fact]
        public async Task IgnoresGuestUsers()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var member = env.TestData.Member;
            member.IsGuest = true;
            member.MemberRoles.Add(new MemberRole { Role = new Role { Name = "Member", Description = "X" } });
            await env.Db.SaveChangesAsync();
            var repository = env.Activate<UserRepository>();

            var archived = await repository.GetActiveMembersQueryable(organization).ToListAsync();

            Assert.Empty(archived);
        }
    }

    public class TheEnsureAndUpdateMemberAsyncMethod
    {
        [Theory]
        [InlineData("U08675309")]
        [InlineData("W08675309")]
        public async Task WithUserEventDeletedFalseUpdatesAvatarOtherFieldsAndSetsMemberAsInactiveAndClearsEmail(
            string userId)
        {
            var env = TestEnvironment.Create();
            var abbot = env.TestData.Abbot;
            var subject = await env.CreateMemberInAgentRoleAsync(
                platformUserId: userId,
                email: "something@example.com");
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            await env.Rooms.AssignMemberAsync(room, subject, RoomRole.FirstResponder, abbot);
            var organization = env.TestData.Organization;
            var slackUserEvent = new UserChangeEvent
            {
                User = new UserInfo
                {
                    Id = userId,
                    TeamId = organization.PlatformId,
                    Deleted = true,
                    Profile = new UserProfile
                    {
                        Email = "newemail@example.com",
                        DisplayName = "my-new-username",
                        RealName = "The Real Deal",
                        Image72 = "https://localhost/avatar"
                    }
                }
            };
            var userEventPayload = UserEventPayload.FromSlackUserInfo(slackUserEvent.User);
            var repository = env.Activate<UserRepository>();

            var result = await repository.EnsureAndUpdateMemberAsync(userEventPayload, organization);

            Assert.NotNull(result);
            Assert.Equal(subject.Id, result.Id);
            Assert.False(result.User.IsBot);
            Assert.False(result.Active);
            Assert.Null(result.User.Email);
            await env.Db.Entry(result).Collection(x => x.RoomAssignments).LoadAsync();
            Assert.Empty(result.MemberRoles);
            Assert.Empty(result.RoomAssignments);
            Assert.Equal("my-new-username", result.DisplayName);
            Assert.Equal(userId, result.User.PlatformUserId);
            Assert.Equal(organization.PlatformId, result.Organization.PlatformId);
        }

        [Fact]
        public async Task WithDeletedFalseDoesNotReactivateUserAndRemovesRolesAndEmail()
        {
            var env = TestEnvironment.Create();
            env.TestData.Member.Active = false;
            await env.Db.SaveChangesAsync();
            var user = env.TestData.User;
            var organization = env.TestData.Organization;
            var slackUserEvent = new UserChangeEvent
            {
                User = new UserInfo
                {
                    Id = user.PlatformUserId,
                    TeamId = organization.PlatformId,
                    Deleted = false,
                    Profile = new UserProfile
                    {
                        Email = "newemail@example.com",
                        DisplayName = "The Real Deal",
                        Image72 = "https://localhost/avatar"
                    }
                }
            };
            var userEventPayload = UserEventPayload.FromSlackUserInfo(slackUserEvent.User);
            var repository = env.Activate<UserRepository>();

            var result = await repository.EnsureAndUpdateMemberAsync(userEventPayload, organization);

            Assert.NotNull(result);
            Assert.False(result.Active);
        }

        [Theory]
        [InlineData("")]
        [InlineData("B0123")]
        public async Task ThrowsForSlackUserWhereIdDoesNotStartWithUOrW(string userId)
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var userEventPayload = UserEventPayload.FromSlackUserInfo(new UserInfo
            {
                Id = userId,
                TeamId = organization.PlatformId,
                Deleted = false,
                Profile = new UserProfile
                {
                    Email = "newemail@example.com",
                    DisplayName = "The Real Deal",
                    Image72 = "https://localhost/avatar"
                }
            });

            var repository = env.Activate<UserRepository>();

            var exception =
                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    repository.EnsureAndUpdateMemberAsync(userEventPayload, organization));
            Assert.Equal($"Shouldn't ever be trying to create a user for a Slack identifier that doesn't start 'U' or 'W'. Identifier {userId} found.", exception.Message);
        }
    }

    public class TheGetPendingMembersQueryableMethod
    {
        [Fact]
        public async Task RetrievesPendingUsersWithNoRoles()
        {
            var env = TestEnvironment.CreateWithoutData();
            var organization = await env.CreateOrganizationAsync();
            organization.Members.Add(new Member
            {
                User = new User
                {
                    DisplayName = "oscar",
                    NameIdentifier = "oauth2|slack|U123",
                    PlatformUserId = "U123",
                    Email = "test@example.com",
                    SlackTeamId = organization.PlatformId
                },
                Active = true
            });
            organization.Members.Add(new Member
            {
                User = new User
                {
                    DisplayName = "takao",
                    NameIdentifier = "oauth2|slack|U125",
                    PlatformUserId = "U125",
                    IsBot = true,
                    Email = "test@example.com"
                },
                Active = true
            });
            organization.Members.Add(new Member
            {
                User = new User
                {
                    DisplayName = "my-guy",
                    NameIdentifier = "oauth2|slack|U124",
                    PlatformUserId = "U124",
                    Email = "test@example.com"
                },
                Active = true,
                MemberRoles = new List<MemberRole> { new() { Role = new Role { Name = "Member", Description = "X" } } }
            });
            await env.Db.SaveChangesAsync();
            var repository = env.Activate<UserRepository>();

            var archived = await repository.GetPendingMembersQueryable(organization).SingleAsync();

            Assert.Equal("U123", archived.User.PlatformUserId);
        }

        [Fact]
        public async Task IgnoresGuestUsers()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var member = env.TestData.Member;
            member.MemberRoles.Clear();
            member.User.NameIdentifier = "some-name-identifier";
            member.Active = true;
            member.IsGuest = true;
            await env.Db.SaveChangesAsync();
            var repository = env.Activate<UserRepository>();

            var archived = await repository.GetPendingMembersQueryable(organization).ToListAsync();

            Assert.Empty(archived);
        }
    }

    public class TheGetArchivedMembersQueryableMethod
    {
        [Fact]
        public async Task RetrievesArchivedUsers()
        {
            var env = TestEnvironment.CreateWithoutData();
            var organization = await env.CreateOrganizationAsync();
            organization.Members.Add(new Member
            {
                User = new User
                {
                    DisplayName = "bigbird",
                    NameIdentifier = "oauth2|slack|U123",
                    PlatformUserId = "U123",
                    Email = "test1@example.com",
                },
                Active = true
            });
            organization.Members.Add(new Member
            {
                User = new User
                {
                    DisplayName = "bert",
                    NameIdentifier = "oauth2|slack|U124",
                    PlatformUserId = "U124",
                    Email = "test2@example.com"
                },
                Active = false
            });
            organization.Members.Add(new Member
            {
                User = new User
                {
                    DisplayName = "ernie",
                    NameIdentifier = null, // Not an archived member because never logged in.
                    PlatformUserId = "U124",
                    Email = "test2@example.com"
                },
                Active = false
            });
            await env.Db.SaveChangesAsync();
            var repository = env.Activate<UserRepository>();

            var archived = await repository.GetArchivedMembersQueryable(organization).SingleAsync();

            Assert.Equal("U124", archived.User.PlatformUserId);
        }

        [Fact]
        public async Task IgnoresBotUsers()
        {
            var env = TestEnvironment.CreateWithoutData();
            var organization = await env.CreateOrganizationAsync();
            organization.Members.Add(new Member
            {
                User = new User
                {
                    DisplayName = "elmo",
                    NameIdentifier = "oauth2|slack|U123",
                    PlatformUserId = "U123",
                    IsBot = true,
                    Email = "test@example.com",
                    SlackTeamId = organization.PlatformId
                },
                Active = false
            });
            await env.Db.SaveChangesAsync();
            var repository = env.Activate<UserRepository>();

            var archived = await repository.GetArchivedMembersQueryable(organization).ToListAsync();

            Assert.Empty(archived);
        }

        [Fact]
        public async Task IgnoresGuestUsers()
        {
            var env = TestEnvironment.CreateWithoutData();
            var organization = await env.CreateOrganizationAsync();
            organization.Members.Add(new Member
            {
                IsGuest = true,
                User = new User
                {
                    DisplayName = "elmo",
                    NameIdentifier = "oauth2|slack|U123",
                    PlatformUserId = "U123",
                    IsBot = false,
                    Email = "test@example.com",
                    SlackTeamId = organization.PlatformId
                },
                Active = false
            });
            await env.Db.SaveChangesAsync();
            var repository = env.Activate<UserRepository>();

            var archived = await repository.GetArchivedMembersQueryable(organization).ToListAsync();

            Assert.Empty(archived);
        }
    }

    public class TheGetUsersNearAsyncMethod
    {
        [Fact]
        public async Task ReturnsUsersWithinSpecifiedRadiusOfLocationButNotThoseFarAwaySortedByUsername()
        {
            var env = TestEnvironment.CreateWithoutData();
            var organization = await env.CreateOrganizationAsync();
            var bellevue = new Point(47.6120759, -122.1112721);
            var me = new Member
            {
                User = new User
                {
                    DisplayName = "bellevue",
                    NameIdentifier = "oauth2|slack|me",
                    PlatformUserId = "me",
                    Email = "me@example.com",
                }
            };
            organization.Members.AddRange(new[]
            {
                me,
                new Member
                {
                    Location = new Point(37.7929789, -122.4212424),
                    User = new User
                    {
                        DisplayName = "sf",
                        NameIdentifier = "oauth2|slack|U123",
                        PlatformUserId = "U123",
                        IsBot = true,
                        Email = "test3@example.com"
                    }
                },
                new Member
                {
                    Location = new Point(47.6062095, -122.3320708),
                    User = new User
                    {
                        DisplayName = "seattle",
                        NameIdentifier = "oauth2|slack|U124",
                        PlatformUserId = "U124",
                        IsBot = true,
                        Email = "test4@example.com"
                    }
                },
                new Member
                {
                    Location = new Point(47.6768927, -122.2059833),
                    User = new User
                    {
                        DisplayName = "kirkland",
                        NameIdentifier = "oauth2|slack|U124",
                        PlatformUserId = "U125",
                        IsBot = true,
                        Email = "test5@example.com"
                    }
                },
                new Member
                {
                    Location = new Point(47.2528768, -122.4442906),
                    User = new User
                    {
                        DisplayName = "tacoma",
                        NameIdentifier = "oauth2|slack|U124",
                        PlatformUserId = "U126",
                        IsBot = true,
                        Email = "test6@example.com"
                    }
                },
            });
            await env.Db.SaveChangesAsync();
            var repository = env.Activate<UserRepository>();

            var near = await repository.GetUsersNearAsync(
                me,
                bellevue,
                40, // 25 miles
                5,
                organization);

            Assert.Equal(2, near.Count);
            Assert.Equal(2, near.TotalCount);
            Assert.Equal("kirkland", near[0].DisplayName);
            Assert.Equal("seattle", near[1].DisplayName);
        }

        [Fact]
        public async Task ReturnsSpecifiedCountOfUsersAlongWithTotalCount()
        {
            var env = TestEnvironment.CreateWithoutData();
            var organization = await env.CreateOrganizationAsync();

            var bellevue = new Point(47.6120759, -122.1112721);
            var me = new Member
            {
                Location = new Point(47.6157847, -122.1151346),
                User = new User
                {
                    DisplayName = "bellevue",
                    NameIdentifier = "oauth2|slack|me",
                    PlatformUserId = "me",
                    Email = "me@example.com",
                }
            };
            organization.Members.AddRange(new[]
            {
                me,
                new Member
                {
                    Location = new Point(37.7929789, -122.4212424),
                    User = new User
                    {
                        DisplayName = "sf",
                        NameIdentifier = "oauth2|slack|U123",
                        PlatformUserId = "U123",
                        IsBot = true,
                        Email = "test3@example.com"
                    }
                },
                new Member
                {
                    Location = new Point(47.6062095, -122.3320708),
                    User = new User
                    {
                        DisplayName = "seattle",
                        NameIdentifier = "oauth2|slack|U124",
                        PlatformUserId = "U124",
                        IsBot = true,
                        Email = "test4@example.com"
                    }
                },
                new Member
                {
                    Location = new Point(47.6768927, -122.2059833),
                    User = new User
                    {
                        DisplayName = "kirkland",
                        NameIdentifier = "oauth2|slack|U124",
                        PlatformUserId = "U125",
                        IsBot = true,
                        Email = "test5@example.com"
                    }
                },
                new Member
                {
                    Location = new Point(47.2528768, -122.4442906),
                    User = new User
                    {
                        DisplayName = "tacoma",
                        NameIdentifier = "oauth2|slack|U124",
                        PlatformUserId = "U126",
                        IsBot = true,
                        Email = "test6@example.com"
                    }
                },
            });
            await env.Db.SaveChangesAsync();
            var repository = env.Activate<UserRepository>();

            var near = await repository.GetUsersNearAsync(
                me,
                bellevue,
                1126, // 700 miles, does not include SF
                2,
                organization);

            Assert.Equal(2, near.Count);
            Assert.Equal(4, near.TotalCount);
        }
    }

    public class TheFindMembersAsyncMethod
    {
        [Theory]
        [InlineData("", 100, new[] { "George", "John", "Paul", "Ringo" }, null)]
        [InlineData("r", 100, new[] { "Ringo", "George", "Paul" }, null)] // Match at the front of the name sorts first (and Paul has an 'r' in his last name)
        [InlineData("r", 100, new[] { "George", "Paul" }, Roles.Agent)] // Only return Members
        [InlineData("o", 100, new[] { "George", "John", "Ringo" }, null)]
        [InlineData("starr", 100, new[] { "Ringo" }, null)] // Searches real name and display name
        [InlineData("o", 2, new[] { "George", "John" }, null)]
        public async Task FindsMembersInExpectedOrder(
            string query,
            int limit,
            string[] matchedDisplayNames,
            string? role)
        {
            var env = TestEnvironment.Create();
            env.Db.Members.Remove(env.TestData.Member);
            await env.Db.SaveChangesAsync();

            // Create test members
            var john = await env.CreateMemberAsync(realName: "John Lennon", displayName: "John");
            var paul = await env.CreateMemberAsync(realName: "Paul McCartney", displayName: "Paul");
            await env.CreateMemberAsync(realName: "Ringo Starr", displayName: "Ringo");
            var george = await env.CreateMemberAsync(realName: "George Harrison", displayName: "George");
            await env.Roles.AddUserToRoleAsync(john, Roles.Agent, env.TestData.Abbot);
            await env.Roles.AddUserToRoleAsync(paul, Roles.Agent, env.TestData.Abbot);
            await env.Roles.AddUserToRoleAsync(george, Roles.Agent, env.TestData.Abbot);

            // Run the query
            var repo = env.Activate<UserRepository>();
            var results = await repo.FindMembersAsync(env.TestData.Organization, query, limit, role);

            Assert.Equal(matchedDisplayNames, results.Select(m => m.DisplayName).ToArray());
        }
    }

    public class TheGetAbbotMemberAsyncMethod
    {
        [Theory]
        [InlineData(null, "abbot")]
        [InlineData("U0", "abbot")]
        [InlineData("U1", "U1")]
        [InlineData("U2", "U2")]
        public async Task ReturnsCurrentAbbotOtherwiseSystemAbbotForTheOrg(string orgBotUserId, string expectedPlatformUserId)
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var someBot = await env.CreateMemberAsync();
            someBot.User.IsBot = true;

            organization.PlatformBotUserId = "U1";
            var abbot1 = await env.CreateMemberAsync(platformUserId: "U1");
            Assert.True(abbot1.User.IsAbbot);

            organization.PlatformBotUserId = "U2";
            var abbot2 = await env.CreateMemberAsync(platformUserId: "U2");
            Assert.True(abbot2.User.IsAbbot);

            organization.PlatformBotUserId = orgBotUserId;
            await env.Db.SaveChangesAsync();
            var repo = env.Activate<UserRepository>();

            var abbot = await repo.EnsureAbbotMemberAsync(organization);

            Assert.Equal(organization, abbot.Organization);
            Assert.Equal(expectedPlatformUserId, abbot.User.PlatformUserId);
        }
    }

    public class TheGetDefaultFirstRespondersMethod
    {
        [Fact]
        public async Task ReturnsMembersInAgentRoleWithIsDefaultResponderSet()
        {
            var env = TestEnvironment.Create();
            var default1 = await env.CreateMemberInAgentRoleAsync();
            default1.IsDefaultFirstResponder = true;
            var default2 = await env.CreateMemberInAgentRoleAsync();
            default2.IsDefaultFirstResponder = true;
            await env.CreateMemberInAgentRoleAsync();
            var notDefault2 = await env.CreateMemberAsync();
            notDefault2.IsDefaultFirstResponder = true;
            await env.Db.SaveChangesAsync();
            var repository = env.Activate<UserRepository>();

            var defaultFirstResponders = await repository.GetDefaultFirstRespondersAsync(env.TestData.Organization);

            Assert.Collection(defaultFirstResponders,
                d => Assert.Equal(d.Id, default1.Id),
                d => Assert.Equal(d.Id, default2.Id));
        }
    }

    public class TheGetApiKeysAsyncMethod
    {
        [Fact]
        public async Task LoadsApiKeys()
        {
            var env = TestEnvironment.Create();
            var member = env.TestData.Member;
            Assert.False(member.ApiKeys.IsLoaded);
            member.ApiKeys.Add(new() { Name = "test", Token = "test" });
            await env.Db.SaveChangesAsync();
            var repository = env.Activate<UserRepository>();

            var result = await repository.GetApiKeysAsync(member);

            Assert.Single(result);
        }
    }

    public class TheArchiveMemberMethod
    {
        [Fact]
        public async Task ThrowsIfActorIsNotAdmin()
        {
            var env = TestEnvironment.Create();
            var actor = env.TestData.Member;
            Assert.False(actor.IsAdministrator());
            var subject = await env.CreateMemberInAgentRoleAsync(email: "foo@example.com");
            var repository = env.Activate<UserRepository>();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => repository.ArchiveMemberAsync(subject, actor));
        }

        [Fact]
        public async Task RemovesMemberAndRoomRolesAndEmailAddressAndDefaultResponderStatus()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var subject = await env.CreateMemberInAgentRoleAsync(email: "foo@example.com");
            var admin = await env.CreateAdminMemberAsync();
            await env.Rooms.AssignMemberAsync(room, subject, RoomRole.EscalationResponder, admin);
            await env.Organizations.AssignDefaultFirstResponderAsync(organization, subject, admin);
            var defaultFirstResponders = await env.Users.GetDefaultFirstRespondersAsync(organization);
            Assert.Equal(subject.Id, Assert.Single(defaultFirstResponders).Id);
            Assert.True(subject.IsAgent());
            await env.Roles.AddUserToRoleAsync(
                subject,
                roleName: Roles.Administrator,
                actor: env.TestData.Abbot);
            var repository = env.Activate<UserRepository>();

            await repository.ArchiveMemberAsync(subject, admin);

            await env.ReloadAsync(subject);
            Assert.Null(subject.User.Email);
            Assert.False(subject.IsAgent());
            Assert.False(subject.Active);
            Assert.Empty(subject.RoomAssignments);
            Assert.Empty(subject.MemberRoles);
            var resultingDefaultResponders = await env.Users.GetDefaultFirstRespondersAsync(organization);
            Assert.Empty(resultingDefaultResponders);
            Assert.False(subject.IsDefaultFirstResponder);
        }
    }

    public class TheUpdateWorkingHoursAsyncMethod
    {
        [Fact]
        public async Task UpdatesPendingNotificationsToLaterAsync()
        {
            var nowUtc = DateTime.Parse(
                "2023-08-12T22:30:00Z",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal);
            var env = TestEnvironment.Create();
            env.Clock.TravelTo(nowUtc);
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var conversation = await env.CreateConversationAsync(room);
            var member = env.TestData.Member;
            await env.Users.UpdateWorkingHoursAsync(
                member,
                new WorkingHours(new TimeOnly(9, 0), new TimeOnly(17, 0)),
                new WorkingDays(
                    Monday: true,
                    Tuesday: true,
                    Wednesday: true,
                    Thursday: true,
                    Friday: true,
                    Saturday: false,
                    Sunday: false));
            var nextWorkingHoursDate = member.GetNextWorkingHoursStartDateUtc(nowUtc);
            Assert.Equal("2023-08-14T16:00:00Z", nextWorkingHoursDate.ToString("yyyy-MM-ddTHH:mm:ssZ"));
            await env.Notifications.EnqueueNotifications(conversation, new[] { member });
            var repository = env.Activate<UserRepository>();

            await repository.UpdateWorkingHoursAsync(
                member,
                new WorkingHours(new TimeOnly(9, 0), new TimeOnly(17, 0)),
                new WorkingDays(
                    Monday: false,
                    Tuesday: true,
                    Wednesday: true,
                    Thursday: true,
                    Friday: true,
                    Saturday: false,
                    Sunday: false));

            var notifications = await env.Db.PendingMemberNotifications.ToListAsync();
            var notification = Assert.Single(notifications);
            Assert.Equal(nextWorkingHoursDate.AddDays(1), notification.NotBeforeUtc);
        }

        [Fact]
        public async Task UpdatesPendingNotificationsToEarlierAsync()
        {
            var nowUtc = DateTime.Parse(
                "2023-08-12T22:30:00Z",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal);
            var env = TestEnvironment.Create();
            env.Clock.TravelTo(nowUtc);
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var conversation = await env.CreateConversationAsync(room);
            var member = env.TestData.Member;
            await env.Users.UpdateWorkingHoursAsync(
                member,
                new WorkingHours(new TimeOnly(9, 0), new TimeOnly(17, 0)),
                new WorkingDays(
                    Monday: true,
                    Tuesday: true,
                    Wednesday: true,
                    Thursday: true,
                    Friday: true,
                    Saturday: false,
                    Sunday: false));
            var nextWorkingHoursDate = member.GetNextWorkingHoursStartDateUtc(nowUtc);
            Assert.Equal("2023-08-14T16:00:00Z", nextWorkingHoursDate.ToString("yyyy-MM-ddTHH:mm:ssZ"));
            await env.Notifications.EnqueueNotifications(conversation, new[] { member });
            var repository = env.Activate<UserRepository>();

            await repository.UpdateWorkingHoursAsync(
                member,
                new WorkingHours(new TimeOnly(9, 0), new TimeOnly(17, 0)),
                new WorkingDays(
                    Monday: true,
                    Tuesday: true,
                    Wednesday: true,
                    Thursday: true,
                    Friday: true,
                    Saturday: false,
                    Sunday: true));

            var notifications = await env.Db.PendingMemberNotifications.ToListAsync();
            var notification = Assert.Single(notifications);
            Assert.Equal(nextWorkingHoursDate.AddDays(-1), notification.NotBeforeUtc);
        }
    }
}
