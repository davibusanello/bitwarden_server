﻿using System.Security.Claims;
using Bit.Api.AdminConsole.Controllers;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.Vault.AuthorizationHandlers.Collections;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Repositories;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Requests;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Controllers;

[ControllerCustomize(typeof(OrganizationUsersController))]
[SutProviderCustomize]
public class OrganizationUsersControllerTests
{
    [Theory]
    [BitAutoData]
    public async Task PutResetPasswordEnrollment_InvitedUser_AcceptsInvite(Guid orgId, Guid userId, OrganizationUserResetPasswordEnrollmentRequestModel model,
        User user, OrganizationUser orgUser, SutProvider<OrganizationUsersController> sutProvider)
    {
        orgUser.Status = Core.Enums.OrganizationUserStatusType.Invited;
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default).ReturnsForAnyArgs(user);
        sutProvider.GetDependency<IUserService>().VerifySecretAsync(default, default).ReturnsForAnyArgs(true);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetByOrganizationAsync(default, default).ReturnsForAnyArgs(orgUser);

        await sutProvider.Sut.PutResetPasswordEnrollment(orgId, userId, model);

        await sutProvider.GetDependency<IAcceptOrgUserCommand>().Received(1).AcceptOrgUserByOrgIdAsync(orgId, user, sutProvider.GetDependency<IUserService>());
    }

    [Theory]
    [BitAutoData]
    public async Task PutResetPasswordEnrollment_ConfirmedUser_AcceptsInvite(Guid orgId, Guid userId, OrganizationUserResetPasswordEnrollmentRequestModel model,
        User user, OrganizationUser orgUser, SutProvider<OrganizationUsersController> sutProvider)
    {
        orgUser.Status = Core.Enums.OrganizationUserStatusType.Confirmed;
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default).ReturnsForAnyArgs(user);
        sutProvider.GetDependency<IUserService>().VerifySecretAsync(default, default).ReturnsForAnyArgs(true);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetByOrganizationAsync(default, default).ReturnsForAnyArgs(orgUser);

        await sutProvider.Sut.PutResetPasswordEnrollment(orgId, userId, model);

        await sutProvider.GetDependency<IAcceptOrgUserCommand>().Received(0).AcceptOrgUserByOrgIdAsync(orgId, user, sutProvider.GetDependency<IUserService>());
    }

    [Theory]
    [BitAutoData]
    public async Task PutResetPasswordEnrollment_PasswordValidationFails_Throws(Guid orgId, Guid userId, OrganizationUserResetPasswordEnrollmentRequestModel model,
        User user, SutProvider<OrganizationUsersController> sutProvider, OrganizationUser orgUser)
    {
        orgUser.Status = OrganizationUserStatusType.Confirmed;
        model.MasterPasswordHash = "NotThePassword";
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default).ReturnsForAnyArgs(user);
        sutProvider.GetDependency<ISsoConfigRepository>().GetByOrganizationIdAsync(default).ReturnsForAnyArgs((SsoConfig)null);
        await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.PutResetPasswordEnrollment(orgId, userId, model));
    }

    [Theory]
    [BitAutoData]
    public async Task PutResetPasswordEnrollment_PasswordValidationPasses_Continues(Guid orgId, Guid userId, OrganizationUserResetPasswordEnrollmentRequestModel model,
        User user, OrganizationUser orgUser, SutProvider<OrganizationUsersController> sutProvider)
    {
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default).ReturnsForAnyArgs(user);
        sutProvider.GetDependency<IUserService>().VerifySecretAsync(user, model.MasterPasswordHash).Returns(true);
        sutProvider.GetDependency<ISsoConfigRepository>().GetByOrganizationIdAsync(default).ReturnsForAnyArgs((SsoConfig)null);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetByOrganizationAsync(default, default).ReturnsForAnyArgs(orgUser);
        await sutProvider.Sut.PutResetPasswordEnrollment(orgId, userId, model);
        await sutProvider.GetDependency<IOrganizationService>().Received(1).UpdateUserResetPasswordEnrollmentAsync(
            orgId,
            userId,
            model.ResetPasswordKey,
            user.Id
        );
    }

    [Theory]
    [BitAutoData]
    public async Task Accept_RequiresKnownUser(Guid orgId, Guid orgUserId, OrganizationUserAcceptRequestModel model,
        SutProvider<OrganizationUsersController> sutProvider)
    {
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default).ReturnsForAnyArgs((User)null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sutProvider.Sut.Accept(orgId, orgUserId, model));
    }

    [Theory]
    [BitAutoData]
    public async Task Accept_NoMasterPasswordReset(Guid orgId, Guid orgUserId,
        OrganizationUserAcceptRequestModel model, User user, SutProvider<OrganizationUsersController> sutProvider)
    {
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default).ReturnsForAnyArgs(user);

        await sutProvider.Sut.Accept(orgId, orgUserId, model);

        await sutProvider.GetDependency<IAcceptOrgUserCommand>().Received(1)
            .AcceptOrgUserByEmailTokenAsync(orgUserId, user, model.Token, sutProvider.GetDependency<IUserService>());
        await sutProvider.GetDependency<IOrganizationService>().DidNotReceiveWithAnyArgs()
            .UpdateUserResetPasswordEnrollmentAsync(default, default, default, default);
    }

    [Theory]
    [BitAutoData]
    public async Task Accept_WhenOrganizationUsePoliciesIsEnabledAndResetPolicyIsEnabled_ShouldHandleResetPassword(Guid orgId, Guid orgUserId,
        OrganizationUserAcceptRequestModel model, User user, SutProvider<OrganizationUsersController> sutProvider)
    {
        // Arrange
        var applicationCacheService = sutProvider.GetDependency<IApplicationCacheService>();
        applicationCacheService.GetOrganizationAbilityAsync(orgId).Returns(new OrganizationAbility { UsePolicies = true });

        var policy = new Policy
        {
            Enabled = true,
            Data = CoreHelpers.ClassToJsonData(new ResetPasswordDataModel { AutoEnrollEnabled = true, }),
        };
        var userService = sutProvider.GetDependency<IUserService>();
        userService.GetUserByPrincipalAsync(default).ReturnsForAnyArgs(user);


        var policyRepository = sutProvider.GetDependency<IPolicyRepository>();
        policyRepository.GetByOrganizationIdTypeAsync(orgId,
            PolicyType.ResetPassword).Returns(policy);

        // Act
        await sutProvider.Sut.Accept(orgId, orgUserId, model);

        // Assert
        await sutProvider.GetDependency<IAcceptOrgUserCommand>().Received(1)
            .AcceptOrgUserByEmailTokenAsync(orgUserId, user, model.Token, userService);
        await sutProvider.GetDependency<IOrganizationService>().Received(1)
            .UpdateUserResetPasswordEnrollmentAsync(orgId, user.Id, model.ResetPasswordKey, user.Id);

        await userService.Received(1).GetUserByPrincipalAsync(default);
        await applicationCacheService.Received(1).GetOrganizationAbilityAsync(orgId);
        await policyRepository.Received(1).GetByOrganizationIdTypeAsync(orgId, PolicyType.ResetPassword);

    }

    [Theory]
    [BitAutoData]
    public async Task Accept_WhenOrganizationUsePoliciesIsDisabled_ShouldNotHandleResetPassword(Guid orgId, Guid orgUserId,
        OrganizationUserAcceptRequestModel model, User user, SutProvider<OrganizationUsersController> sutProvider)
    {
        // Arrange
        var applicationCacheService = sutProvider.GetDependency<IApplicationCacheService>();
        applicationCacheService.GetOrganizationAbilityAsync(orgId).Returns(new OrganizationAbility { UsePolicies = false });

        var policy = new Policy
        {
            Enabled = true,
            Data = CoreHelpers.ClassToJsonData(new ResetPasswordDataModel { AutoEnrollEnabled = true, }),
        };
        var userService = sutProvider.GetDependency<IUserService>();
        userService.GetUserByPrincipalAsync(default).ReturnsForAnyArgs(user);

        var policyRepository = sutProvider.GetDependency<IPolicyRepository>();
        policyRepository.GetByOrganizationIdTypeAsync(orgId,
            PolicyType.ResetPassword).Returns(policy);

        // Act
        await sutProvider.Sut.Accept(orgId, orgUserId, model);

        // Assert
        await userService.Received(1).GetUserByPrincipalAsync(default);
        await sutProvider.GetDependency<IAcceptOrgUserCommand>().Received(1)
            .AcceptOrgUserByEmailTokenAsync(orgUserId, user, model.Token, userService);
        await sutProvider.GetDependency<IOrganizationService>().Received(0)
            .UpdateUserResetPasswordEnrollmentAsync(orgId, user.Id, model.ResetPasswordKey, user.Id);

        await policyRepository.Received(0).GetByOrganizationIdTypeAsync(orgId, PolicyType.ResetPassword);
        await applicationCacheService.Received(1).GetOrganizationAbilityAsync(orgId);
    }

    [Theory]
    [BitAutoData]
    public async Task Invite_Success(OrganizationAbility organizationAbility, OrganizationUserInviteRequestModel model,
        Guid userId, SutProvider<OrganizationUsersController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageUsers(organizationAbility.Id).Returns(true);
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilityAsync(organizationAbility.Id)
            .Returns(organizationAbility);
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(),
                Arg.Any<IEnumerable<Collection>>(),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs => reqs.Contains(BulkCollectionOperations.ModifyUserAccess)))
            .Returns(AuthorizationResult.Success());
        sutProvider.GetDependency<IUserService>().GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns(userId);

        await sutProvider.Sut.Invite(organizationAbility.Id, model);

        await sutProvider.GetDependency<IOrganizationService>().Received(1).InviteUsersAsync(organizationAbility.Id,
            userId, systemUser: null, Arg.Is<IEnumerable<(OrganizationUserInvite, string)>>(invites =>
                invites.Count() == 1 &&
                invites.First().Item1.Emails.SequenceEqual(model.Emails) &&
                invites.First().Item1.Type == model.Type &&
                invites.First().Item1.AccessSecretsManager == model.AccessSecretsManager));
    }

    [Theory]
    [BitAutoData]
    public async Task Invite_NotAuthorizedToGiveAccessToCollections_Throws(OrganizationAbility organizationAbility, OrganizationUserInviteRequestModel model,
        Guid userId, SutProvider<OrganizationUsersController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageUsers(organizationAbility.Id).Returns(true);
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilityAsync(organizationAbility.Id)
            .Returns(organizationAbility);
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(),
                Arg.Any<IEnumerable<Collection>>(),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs => reqs.Contains(BulkCollectionOperations.ModifyUserAccess)))
            .Returns(AuthorizationResult.Failed());
        sutProvider.GetDependency<IUserService>().GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns(userId);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.Invite(organizationAbility.Id, model));
    }

    [Theory, BitAutoData]
    public async Task Get_ReturnsUser(
        OrganizationUserUserDetails organizationUser, ICollection<CollectionAccessSelection> collections,
        SutProvider<OrganizationUsersController> sutProvider)
    {
        organizationUser.Permissions = null;

        sutProvider.GetDependency<ICurrentContext>()
            .ManageUsers(organizationUser.OrganizationId)
            .Returns(true);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetDetailsByIdWithCollectionsAsync(organizationUser.Id)
            .Returns((organizationUser, collections));

        sutProvider.GetDependency<IGetOrganizationUsersClaimedStatusQuery>()
            .GetUsersOrganizationClaimedStatusAsync(organizationUser.OrganizationId, Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(organizationUser.Id)))
            .Returns(new Dictionary<Guid, bool> { { organizationUser.Id, true } });

        var response = await sutProvider.Sut.Get(organizationUser.OrganizationId, organizationUser.Id, false);

        Assert.Equal(organizationUser.Id, response.Id);
        Assert.True(response.ManagedByOrganization);
        Assert.True(response.ClaimedByOrganization);
    }

    [Theory]
    [BitAutoData]
    public async Task GetMany_ReturnsUsers(
        ICollection<OrganizationUserUserDetails> organizationUsers, OrganizationAbility organizationAbility,
        SutProvider<OrganizationUsersController> sutProvider)
    {
        GetMany_Setup(organizationAbility, organizationUsers, sutProvider);
        var response = await sutProvider.Sut.Get(organizationAbility.Id, false, false);

        Assert.True(response.Data.All(r => organizationUsers.Any(ou => ou.Id == r.Id)));
    }

    [Theory]
    [BitAutoData]
    public async Task GetAccountRecoveryDetails_ReturnsDetails(
        Guid organizationId,
        OrganizationUserBulkRequestModel bulkRequestModel,
        ICollection<OrganizationUserResetPasswordDetails> resetPasswordDetails,
        SutProvider<OrganizationUsersController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageResetPassword(organizationId).Returns(true);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAccountRecoveryDetailsByOrganizationUserAsync(organizationId, bulkRequestModel.Ids)
            .Returns(resetPasswordDetails);

        var response = await sutProvider.Sut.GetAccountRecoveryDetails(organizationId, bulkRequestModel);

        Assert.Equal(resetPasswordDetails.Count, response.Data.Count());
        Assert.True(response.Data.All(r =>
            resetPasswordDetails.Any(ou =>
                ou.OrganizationUserId == r.OrganizationUserId &&
                ou.Kdf == r.Kdf &&
                ou.KdfIterations == r.KdfIterations &&
                ou.KdfMemory == r.KdfMemory &&
                ou.KdfParallelism == r.KdfParallelism &&
                ou.ResetPasswordKey == r.ResetPasswordKey &&
                ou.EncryptedPrivateKey == r.EncryptedPrivateKey)));
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteAccount_WhenUserCanManageUsers_Success(
        Guid orgId, Guid id, User currentUser, SutProvider<OrganizationUsersController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageUsers(orgId).Returns(true);
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default).ReturnsForAnyArgs(currentUser);

        await sutProvider.Sut.DeleteAccount(orgId, id);

        await sutProvider.GetDependency<IDeleteClaimedOrganizationUserAccountCommand>()
            .Received(1)
            .DeleteUserAsync(orgId, id, currentUser.Id);
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteAccount_WhenCurrentUserNotFound_ThrowsUnauthorizedAccessException(
        Guid orgId, Guid id, SutProvider<OrganizationUsersController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageUsers(orgId).Returns(true);
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default).ReturnsForAnyArgs((User)null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sutProvider.Sut.DeleteAccount(orgId, id));
    }

    [Theory]
    [BitAutoData]
    public async Task BulkDeleteAccount_WhenUserCanManageUsers_Success(
        Guid orgId, OrganizationUserBulkRequestModel model, User currentUser,
        List<(Guid, string)> deleteResults, SutProvider<OrganizationUsersController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageUsers(orgId).Returns(true);
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default).ReturnsForAnyArgs(currentUser);
        sutProvider.GetDependency<IDeleteClaimedOrganizationUserAccountCommand>()
            .DeleteManyUsersAsync(orgId, model.Ids, currentUser.Id)
            .Returns(deleteResults);

        var response = await sutProvider.Sut.BulkDeleteAccount(orgId, model);

        Assert.Equal(deleteResults.Count, response.Data.Count());
        Assert.True(response.Data.All(r => deleteResults.Any(res => res.Item1 == r.Id && res.Item2 == r.Error)));
        await sutProvider.GetDependency<IDeleteClaimedOrganizationUserAccountCommand>()
            .Received(1)
            .DeleteManyUsersAsync(orgId, model.Ids, currentUser.Id);
    }

    [Theory]
    [BitAutoData]
    public async Task BulkDeleteAccount_WhenCurrentUserNotFound_ThrowsUnauthorizedAccessException(
        Guid orgId, OrganizationUserBulkRequestModel model, SutProvider<OrganizationUsersController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageUsers(orgId).Returns(true);
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default).ReturnsForAnyArgs((User)null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sutProvider.Sut.BulkDeleteAccount(orgId, model));
    }

    private void GetMany_Setup(OrganizationAbility organizationAbility,
        ICollection<OrganizationUserUserDetails> organizationUsers,
        SutProvider<OrganizationUsersController> sutProvider)
    {
        foreach (var orgUser in organizationUsers)
        {
            orgUser.Permissions = null;
        }
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilityAsync(organizationAbility.Id)
            .Returns(organizationAbility);

        sutProvider.GetDependency<IOrganizationUserUserDetailsQuery>().GetOrganizationUserUserDetails(Arg.Any<OrganizationUserUserDetailsQueryRequest>()).Returns(organizationUsers);

        sutProvider.GetDependency<IAuthorizationService>().AuthorizeAsync(
            user: Arg.Any<ClaimsPrincipal>(),
            resource: Arg.Any<Object>(),
            requirements: Arg.Any<IEnumerable<IAuthorizationRequirement>>())
            .Returns(AuthorizationResult.Success());

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(organizationAbility.Id, Arg.Any<bool>(), Arg.Any<bool>())
            .Returns(organizationUsers);
    }

    [Theory]
    [BitAutoData]
    public async Task Accept_WhenOrganizationUsePoliciesIsEnabledAndResetPolicyIsEnabled_WithPolicyRequirementsEnabled_ShouldHandleResetPassword(Guid orgId, Guid orgUserId,
        OrganizationUserAcceptRequestModel model, User user, SutProvider<OrganizationUsersController> sutProvider)
    {
        // Arrange
        var applicationCacheService = sutProvider.GetDependency<IApplicationCacheService>();
        applicationCacheService.GetOrganizationAbilityAsync(orgId).Returns(new OrganizationAbility { UsePolicies = true });

        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.PolicyRequirements).Returns(true);

        var policy = new Policy
        {
            Enabled = true,
            Data = CoreHelpers.ClassToJsonData(new ResetPasswordDataModel { AutoEnrollEnabled = true, }),
        };
        var userService = sutProvider.GetDependency<IUserService>();
        userService.GetUserByPrincipalAsync(default).ReturnsForAnyArgs(user);

        var policyRequirementQuery = sutProvider.GetDependency<IPolicyRequirementQuery>();

        var policyRepository = sutProvider.GetDependency<IPolicyRepository>();

        var policyRequirement = new ResetPasswordPolicyRequirement { AutoEnrollOrganizations = [orgId] };

        policyRequirementQuery.GetAsync<ResetPasswordPolicyRequirement>(user.Id).Returns(policyRequirement);

        // Act
        await sutProvider.Sut.Accept(orgId, orgUserId, model);

        // Assert
        await sutProvider.GetDependency<IAcceptOrgUserCommand>().Received(1)
            .AcceptOrgUserByEmailTokenAsync(orgUserId, user, model.Token, userService);
        await sutProvider.GetDependency<IOrganizationService>().Received(1)
            .UpdateUserResetPasswordEnrollmentAsync(orgId, user.Id, model.ResetPasswordKey, user.Id);

        await userService.Received(1).GetUserByPrincipalAsync(default);
        await applicationCacheService.Received(0).GetOrganizationAbilityAsync(orgId);
        await policyRepository.Received(0).GetByOrganizationIdTypeAsync(orgId, PolicyType.ResetPassword);
        await policyRequirementQuery.Received(1).GetAsync<ResetPasswordPolicyRequirement>(user.Id);
        Assert.True(policyRequirement.AutoEnrollEnabled(orgId));
    }

    [Theory]
    [BitAutoData]
    public async Task Accept_WithInvalidModelResetPasswordKey_WithPolicyRequirementsEnabled_ThrowsBadRequestException(Guid orgId, Guid orgUserId,
        OrganizationUserAcceptRequestModel model, User user, SutProvider<OrganizationUsersController> sutProvider)
    {
        // Arrange
        model.ResetPasswordKey = " ";
        var applicationCacheService = sutProvider.GetDependency<IApplicationCacheService>();
        applicationCacheService.GetOrganizationAbilityAsync(orgId).Returns(new OrganizationAbility { UsePolicies = true });

        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.PolicyRequirements).Returns(true);

        var policy = new Policy
        {
            Enabled = true,
            Data = CoreHelpers.ClassToJsonData(new ResetPasswordDataModel { AutoEnrollEnabled = true, }),
        };
        var userService = sutProvider.GetDependency<IUserService>();
        userService.GetUserByPrincipalAsync(default).ReturnsForAnyArgs(user);

        var policyRepository = sutProvider.GetDependency<IPolicyRepository>();

        var policyRequirementQuery = sutProvider.GetDependency<IPolicyRequirementQuery>();

        var policyRequirement = new ResetPasswordPolicyRequirement { AutoEnrollOrganizations = [orgId] };

        policyRequirementQuery.GetAsync<ResetPasswordPolicyRequirement>(user.Id).Returns(policyRequirement);

        // Act
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
        sutProvider.Sut.Accept(orgId, orgUserId, model));

        // Assert
        await sutProvider.GetDependency<IAcceptOrgUserCommand>().Received(0)
            .AcceptOrgUserByEmailTokenAsync(orgUserId, user, model.Token, userService);
        await sutProvider.GetDependency<IOrganizationService>().Received(0)
            .UpdateUserResetPasswordEnrollmentAsync(orgId, user.Id, model.ResetPasswordKey, user.Id);

        await userService.Received(1).GetUserByPrincipalAsync(default);
        await applicationCacheService.Received(0).GetOrganizationAbilityAsync(orgId);
        await policyRepository.Received(0).GetByOrganizationIdTypeAsync(orgId, PolicyType.ResetPassword);
        await policyRequirementQuery.Received(1).GetAsync<ResetPasswordPolicyRequirement>(user.Id);

        Assert.Equal("Master Password reset is required, but not provided.", exception.Message);
    }
}
