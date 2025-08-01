﻿// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Api.Models.Request;
using Bit.Api.Models.Request.Organizations;
using Bit.Api.Utilities;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;
using Bit.Core.Billing.Organizations.Commands;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Organizations.Queries;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.OrganizationConnectionConfigs;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Controllers.SelfHosted;

[Route("organizations/licenses/self-hosted")]
[Authorize("Application")]
[SelfHosted(SelfHostedOnly = true)]
public class SelfHostedOrganizationLicensesController : Controller
{
    private readonly ICurrentContext _currentContext;
    private readonly IGetSelfHostedOrganizationLicenseQuery _getSelfHostedOrganizationLicenseQuery;
    private readonly IOrganizationConnectionRepository _organizationConnectionRepository;
    private readonly ISelfHostedOrganizationSignUpCommand _selfHostedOrganizationSignUpCommand;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IUserService _userService;
    private readonly IUpdateOrganizationLicenseCommand _updateOrganizationLicenseCommand;

    public SelfHostedOrganizationLicensesController(
        ICurrentContext currentContext,
        IGetSelfHostedOrganizationLicenseQuery getSelfHostedOrganizationLicenseQuery,
        IOrganizationConnectionRepository organizationConnectionRepository,
        ISelfHostedOrganizationSignUpCommand selfHostedOrganizationSignUpCommand,
        IOrganizationRepository organizationRepository,
        IUserService userService,
        IUpdateOrganizationLicenseCommand updateOrganizationLicenseCommand)
    {
        _currentContext = currentContext;
        _getSelfHostedOrganizationLicenseQuery = getSelfHostedOrganizationLicenseQuery;
        _organizationConnectionRepository = organizationConnectionRepository;
        _selfHostedOrganizationSignUpCommand = selfHostedOrganizationSignUpCommand;
        _organizationRepository = organizationRepository;
        _userService = userService;
        _updateOrganizationLicenseCommand = updateOrganizationLicenseCommand;
    }

    [HttpPost("")]
    public async Task<OrganizationResponseModel> PostLicenseAsync(OrganizationCreateLicenseRequestModel model)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        var license = await ApiHelpers.ReadJsonFileFromBody<OrganizationLicense>(HttpContext, model.License);
        if (license == null)
        {
            throw new BadRequestException("Invalid license");
        }

        var result = await _selfHostedOrganizationSignUpCommand.SignUpAsync(license, user, model.Key,
            model.CollectionName, model.Keys?.PublicKey, model.Keys?.EncryptedPrivateKey);

        return new OrganizationResponseModel(result.Item1, null);
    }

    [HttpPost("{id}")]
    public async Task PostLicenseAsync(string id, LicenseRequestModel model)
    {
        var orgIdGuid = new Guid(id);
        if (!await _currentContext.OrganizationOwner(orgIdGuid))
        {
            throw new NotFoundException();
        }

        var license = await ApiHelpers.ReadJsonFileFromBody<OrganizationLicense>(HttpContext, model.License);
        if (license == null)
        {
            throw new BadRequestException("Invalid license");
        }

        var selfHostedOrganizationDetails = await _organizationRepository.GetSelfHostedOrganizationDetailsById(orgIdGuid);
        if (selfHostedOrganizationDetails == null)
        {
            throw new NotFoundException();
        }

        var currentOrganization = await _organizationRepository.GetByLicenseKeyAsync(license.LicenseKey);

        await _updateOrganizationLicenseCommand.UpdateLicenseAsync(selfHostedOrganizationDetails, license, currentOrganization);
    }

    [HttpPost("{id}/sync")]
    public async Task SyncLicenseAsync(string id)
    {
        var selfHostedOrganizationDetails = await _organizationRepository.GetSelfHostedOrganizationDetailsById(new Guid(id));
        if (selfHostedOrganizationDetails == null)
        {
            throw new NotFoundException();
        }

        if (!await _currentContext.OrganizationOwner(selfHostedOrganizationDetails.Id))
        {
            throw new NotFoundException();
        }

        var billingSyncConnection =
            (await _organizationConnectionRepository.GetByOrganizationIdTypeAsync(selfHostedOrganizationDetails.Id,
                OrganizationConnectionType.CloudBillingSync)).FirstOrDefault();
        if (billingSyncConnection == null)
        {
            throw new NotFoundException("Unable to get Cloud Billing Sync connection");
        }

        var license =
            await _getSelfHostedOrganizationLicenseQuery.GetLicenseAsync(selfHostedOrganizationDetails, billingSyncConnection);
        var currentOrganization = await _organizationRepository.GetByLicenseKeyAsync(license.LicenseKey);

        await _updateOrganizationLicenseCommand.UpdateLicenseAsync(selfHostedOrganizationDetails, license, currentOrganization);

        var config = billingSyncConnection.GetConfig<BillingSyncConfig>();
        config.LastLicenseSync = DateTime.Now;
        billingSyncConnection.SetConfig(config);
        await _organizationConnectionRepository.ReplaceAsync(billingSyncConnection);
    }
}
