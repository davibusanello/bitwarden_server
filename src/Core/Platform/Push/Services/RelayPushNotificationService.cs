﻿#nullable enable
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Entities;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.IdentityServer;
using Bit.Core.Models;
using Bit.Core.Models.Api;
using Bit.Core.NotificationCenter.Entities;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Tools.Entities;
using Bit.Core.Vault.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Platform.Push.Internal;

/// <summary>
/// Sends mobile push notifications to the Bitwarden Cloud API, then relayed to Azure Notification Hub.
/// Used by Self-Hosted environments.
/// Received by PushController endpoint in Api project.
/// </summary>
public class RelayPushNotificationService : BaseIdentityClientService, IPushNotificationService
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly IGlobalSettings _globalSettings;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly TimeProvider _timeProvider;

    public RelayPushNotificationService(
        IHttpClientFactory httpFactory,
        IDeviceRepository deviceRepository,
        GlobalSettings globalSettings,
        IHttpContextAccessor httpContextAccessor,
        ILogger<RelayPushNotificationService> logger,
        TimeProvider timeProvider)
        : base(
            httpFactory,
            globalSettings.PushRelayBaseUri,
            globalSettings.Installation.IdentityUri,
            ApiScopes.ApiPush,
            $"installation.{globalSettings.Installation.Id}",
            globalSettings.Installation.Key,
            logger)
    {
        _deviceRepository = deviceRepository;
        _globalSettings = globalSettings;
        _httpContextAccessor = httpContextAccessor;
        _timeProvider = timeProvider;
    }

    public async Task PushSyncCipherCreateAsync(Cipher cipher, IEnumerable<Guid> collectionIds)
    {
        await PushCipherAsync(cipher, PushType.SyncCipherCreate, collectionIds);
    }

    public async Task PushSyncCipherUpdateAsync(Cipher cipher, IEnumerable<Guid> collectionIds)
    {
        await PushCipherAsync(cipher, PushType.SyncCipherUpdate, collectionIds);
    }

    public async Task PushSyncCipherDeleteAsync(Cipher cipher)
    {
        await PushCipherAsync(cipher, PushType.SyncLoginDelete, null);
    }

    private async Task PushCipherAsync(Cipher cipher, PushType type, IEnumerable<Guid>? collectionIds)
    {
        if (cipher.OrganizationId.HasValue)
        {
            // We cannot send org pushes since access logic is much more complicated than just the fact that they belong
            // to the organization. Potentially we could blindly send to just users that have the access all permission
            // device registration needs to be more granular to handle that appropriately. A more brute force approach could
            // me to send "full sync" push to all org users, but that has the potential to DDOS the API in bursts.

            // await SendPayloadToOrganizationAsync(cipher.OrganizationId.Value, type, message, true);
        }
        else if (cipher.UserId.HasValue)
        {
            var message = new SyncCipherPushNotification
            {
                Id = cipher.Id,
                UserId = cipher.UserId,
                OrganizationId = cipher.OrganizationId,
                RevisionDate = cipher.RevisionDate,
            };

            await SendPayloadToUserAsync(cipher.UserId.Value, type, message, true);
        }
    }

    public async Task PushSyncFolderCreateAsync(Folder folder)
    {
        await PushFolderAsync(folder, PushType.SyncFolderCreate);
    }

    public async Task PushSyncFolderUpdateAsync(Folder folder)
    {
        await PushFolderAsync(folder, PushType.SyncFolderUpdate);
    }

    public async Task PushSyncFolderDeleteAsync(Folder folder)
    {
        await PushFolderAsync(folder, PushType.SyncFolderDelete);
    }

    private async Task PushFolderAsync(Folder folder, PushType type)
    {
        var message = new SyncFolderPushNotification
        {
            Id = folder.Id,
            UserId = folder.UserId,
            RevisionDate = folder.RevisionDate
        };

        await SendPayloadToUserAsync(folder.UserId, type, message, true);
    }

    public async Task PushSyncCiphersAsync(Guid userId)
    {
        await PushUserAsync(userId, PushType.SyncCiphers);
    }

    public async Task PushSyncVaultAsync(Guid userId)
    {
        await PushUserAsync(userId, PushType.SyncVault);
    }

    public async Task PushSyncOrganizationsAsync(Guid userId)
    {
        await PushUserAsync(userId, PushType.SyncOrganizations);
    }

    public async Task PushSyncOrgKeysAsync(Guid userId)
    {
        await PushUserAsync(userId, PushType.SyncOrgKeys);
    }

    public async Task PushSyncSettingsAsync(Guid userId)
    {
        await PushUserAsync(userId, PushType.SyncSettings);
    }

    public async Task PushLogOutAsync(Guid userId, bool excludeCurrentContext = false)
    {
        await PushUserAsync(userId, PushType.LogOut, excludeCurrentContext);
    }

    private async Task PushUserAsync(Guid userId, PushType type, bool excludeCurrentContext = false)
    {
        var message = new UserPushNotification { UserId = userId, Date = _timeProvider.GetUtcNow().UtcDateTime };

        await SendPayloadToUserAsync(userId, type, message, excludeCurrentContext);
    }

    public async Task PushSyncSendCreateAsync(Send send)
    {
        await PushSendAsync(send, PushType.SyncSendCreate);
    }

    public async Task PushSyncSendUpdateAsync(Send send)
    {
        await PushSendAsync(send, PushType.SyncSendUpdate);
    }

    public async Task PushSyncSendDeleteAsync(Send send)
    {
        await PushSendAsync(send, PushType.SyncSendDelete);
    }

    private async Task PushSendAsync(Send send, PushType type)
    {
        if (send.UserId.HasValue)
        {
            var message = new SyncSendPushNotification
            {
                Id = send.Id,
                UserId = send.UserId.Value,
                RevisionDate = send.RevisionDate
            };

            await SendPayloadToUserAsync(message.UserId, type, message, true);
        }
    }

    public async Task PushAuthRequestAsync(AuthRequest authRequest)
    {
        await PushAuthRequestAsync(authRequest, PushType.AuthRequest);
    }

    public async Task PushAuthRequestResponseAsync(AuthRequest authRequest)
    {
        await PushAuthRequestAsync(authRequest, PushType.AuthRequestResponse);
    }

    private async Task PushAuthRequestAsync(AuthRequest authRequest, PushType type)
    {
        var message = new AuthRequestPushNotification { Id = authRequest.Id, UserId = authRequest.UserId };

        await SendPayloadToUserAsync(authRequest.UserId, type, message, true);
    }

    public async Task PushNotificationAsync(Notification notification)
    {
        var message = new NotificationPushNotification
        {
            Id = notification.Id,
            Priority = notification.Priority,
            Global = notification.Global,
            ClientType = notification.ClientType,
            UserId = notification.UserId,
            OrganizationId = notification.OrganizationId,
            InstallationId = notification.Global ? _globalSettings.Installation.Id : null,
            TaskId = notification.TaskId,
            Title = notification.Title,
            Body = notification.Body,
            CreationDate = notification.CreationDate,
            RevisionDate = notification.RevisionDate
        };

        if (notification.Global)
        {
            await SendPayloadToInstallationAsync(PushType.Notification, message, true, notification.ClientType);
        }
        else if (notification.UserId.HasValue)
        {
            await SendPayloadToUserAsync(notification.UserId.Value, PushType.Notification, message, true,
                notification.ClientType);
        }
        else if (notification.OrganizationId.HasValue)
        {
            await SendPayloadToOrganizationAsync(notification.OrganizationId.Value, PushType.Notification, message,
                true, notification.ClientType);
        }
        else
        {
            _logger.LogWarning("Invalid notification id {NotificationId} push notification", notification.Id);
        }
    }

    public async Task PushNotificationStatusAsync(Notification notification, NotificationStatus notificationStatus)
    {
        var message = new NotificationPushNotification
        {
            Id = notification.Id,
            Priority = notification.Priority,
            Global = notification.Global,
            ClientType = notification.ClientType,
            UserId = notification.UserId,
            OrganizationId = notification.OrganizationId,
            InstallationId = notification.Global ? _globalSettings.Installation.Id : null,
            TaskId = notification.TaskId,
            Title = notification.Title,
            Body = notification.Body,
            CreationDate = notification.CreationDate,
            RevisionDate = notification.RevisionDate,
            ReadDate = notificationStatus.ReadDate,
            DeletedDate = notificationStatus.DeletedDate
        };

        if (notification.Global)
        {
            await SendPayloadToInstallationAsync(PushType.NotificationStatus, message, true, notification.ClientType);
        }
        else if (notification.UserId.HasValue)
        {
            await SendPayloadToUserAsync(notification.UserId.Value, PushType.NotificationStatus, message, true,
                notification.ClientType);
        }
        else if (notification.OrganizationId.HasValue)
        {
            await SendPayloadToOrganizationAsync(notification.OrganizationId.Value, PushType.NotificationStatus, message,
                true, notification.ClientType);
        }
        else
        {
            _logger.LogWarning("Invalid notification status id {NotificationId} push notification", notification.Id);
        }
    }

    public async Task PushSyncOrganizationStatusAsync(Organization organization)
    {
        var message = new OrganizationStatusPushNotification
        {
            OrganizationId = organization.Id,
            Enabled = organization.Enabled
        };

        await SendPayloadToOrganizationAsync(organization.Id, PushType.SyncOrganizationStatusChanged, message, false);
    }

    public async Task PushSyncOrganizationCollectionManagementSettingsAsync(Organization organization) =>
        await SendPayloadToOrganizationAsync(
            organization.Id,
            PushType.SyncOrganizationCollectionSettingChanged,
            new OrganizationCollectionManagementPushNotification
            {
                OrganizationId = organization.Id,
                LimitCollectionCreation = organization.LimitCollectionCreation,
                LimitCollectionDeletion = organization.LimitCollectionDeletion,
                LimitItemDeletion = organization.LimitItemDeletion
            },
            false
        );

    public async Task PushPendingSecurityTasksAsync(Guid userId)
    {
        await PushUserAsync(userId, PushType.PendingSecurityTasks);
    }

    private async Task SendPayloadToInstallationAsync(PushType type, object payload, bool excludeCurrentContext,
        ClientType? clientType = null)
    {
        var request = new PushSendRequestModel
        {
            InstallationId = _globalSettings.Installation.Id.ToString(),
            Type = type,
            Payload = payload,
            ClientType = clientType
        };

        await AddCurrentContextAsync(request, excludeCurrentContext);
        await SendAsync(HttpMethod.Post, "push/send", request);
    }

    private async Task SendPayloadToUserAsync(Guid userId, PushType type, object payload, bool excludeCurrentContext,
        ClientType? clientType = null)
    {
        var request = new PushSendRequestModel
        {
            UserId = userId.ToString(),
            Type = type,
            Payload = payload,
            ClientType = clientType
        };

        await AddCurrentContextAsync(request, excludeCurrentContext);
        await SendAsync(HttpMethod.Post, "push/send", request);
    }

    private async Task SendPayloadToOrganizationAsync(Guid orgId, PushType type, object payload,
        bool excludeCurrentContext, ClientType? clientType = null)
    {
        var request = new PushSendRequestModel
        {
            OrganizationId = orgId.ToString(),
            Type = type,
            Payload = payload,
            ClientType = clientType
        };

        await AddCurrentContextAsync(request, excludeCurrentContext);
        await SendAsync(HttpMethod.Post, "push/send", request);
    }

    private async Task AddCurrentContextAsync(PushSendRequestModel request, bool addIdentifier)
    {
        var currentContext =
            _httpContextAccessor.HttpContext?.RequestServices.GetService(typeof(ICurrentContext)) as ICurrentContext;
        if (!string.IsNullOrWhiteSpace(currentContext?.DeviceIdentifier))
        {
            var device = await _deviceRepository.GetByIdentifierAsync(currentContext.DeviceIdentifier);
            if (device != null)
            {
                request.DeviceId = device.Id.ToString();
            }

            if (addIdentifier)
            {
                request.Identifier = currentContext.DeviceIdentifier;
            }
        }
    }

    public Task SendPayloadToInstallationAsync(string installationId, PushType type, object payload, string? identifier,
        string? deviceId = null, ClientType? clientType = null) =>
        throw new NotImplementedException();

    public Task SendPayloadToUserAsync(string userId, PushType type, object payload, string? identifier,
        string? deviceId = null, ClientType? clientType = null)
    {
        throw new NotImplementedException();
    }

    public Task SendPayloadToOrganizationAsync(string orgId, PushType type, object payload, string? identifier,
        string? deviceId = null, ClientType? clientType = null)
    {
        throw new NotImplementedException();
    }
}
