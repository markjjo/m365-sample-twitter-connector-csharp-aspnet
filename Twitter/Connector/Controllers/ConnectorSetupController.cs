﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace Sample.TwitterConnector
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Web.Http;
    using System.Linq.Expressions;
    using Microsoft.WindowsAzure.Storage.Table;
    using Connector;
    using System.Net;
    using Sample.TwitterSDK;

    /// <summary>
    /// API controller for all native connector setups
    /// </summary>
    [ApiAuthorizationModule]
    public class ConnectorSetupController : ApiController
    {
        private AzureTableProvider azureTableProvider;
        private IConnectorSourceProvider connectorSourceProvider;

        public ConnectorSetupController()
        {
            azureTableProvider = new AzureTableProvider(Settings.StorageAccountConnectionString);
            var client = new RestApiRepository(SettingsTwitter.TwitterAuthEndPoint);
            connectorSourceProvider = new TwitterProvider(azureTableProvider, client, new TwitterAuthProvider(client, azureTableProvider));
        }

        /// <summary>
        /// Validate Connector Setup 
        /// </summary>
        /// <returns>true for validation success</returns>
        [HttpGet]
        [Route("preview/ValidateSetup")]
        [Route("api/ConnectorSetup/ValidateSetup")]
        public Task<bool> ValidateSetup()
        {
            return Task.FromResult(true);
        }

        /// <summary>
        /// Final job setup for Connector service
        /// </summary>
        /// <param name="jobId">job Id</param>
        /// <returns></returns>
        [HttpGet]
        [Route("preview/OnJobCreated")]
        [Route("api/ConnectorSetup/ConnectorOAuth")]
        public async Task<bool> ConnectorOAuth([FromUri] string jobId)
        {
            CloudTable jobMappingTable = azureTableProvider.GetAzureTableReference(Settings.PageJobMappingTableName);
            
            Expression<Func<PageJobEntity, bool>> filter = (entity => entity.RowKey == jobId);
            List<PageJobEntity> pageJobEntityList = await azureTableProvider.QueryEntitiesAsync<PageJobEntity>(jobMappingTable, filter);

            if (!pageJobEntityList.Any())
            {
                return false;
            }

            Trace.TraceInformation("Job with JobId: {0} successfully set up", jobId);
            PageJobEntity pageJobEntity = pageJobEntityList?[0];
            return true;
        }

        /// <summary>
        /// Delete job page
        /// </summary>
        /// <param name="jobId">job Id</param>
        /// <returns>success or failure</returns>
        [HttpDelete]
        [Route("preview/OnJobDeleted")]
        [Route("api/ConnectorSetup/DeleteJob")]
        public async Task<HttpStatusCode> DeleteJob([FromUri] string jobId)
        {
            CloudTable jobMappingTable = azureTableProvider.GetAzureTableReference(Settings.PageJobMappingTableName);

            Expression<Func<PageJobEntity, bool>> filter = (entity => entity.RowKey == jobId);
            List<PageJobEntity> pageJobEntityList = await azureTableProvider.QueryEntitiesAsync<PageJobEntity>(jobMappingTable, filter);

            if (!pageJobEntityList.Any())
            {
                return HttpStatusCode.NotFound;
            }
            PageJobEntity pageJobEntity = pageJobEntityList?[0];
           
            bool unsubscribed = await connectorSourceProvider.Unsubscribe(pageJobEntity.SourceInfo);
            Trace.TraceInformation("Job with JobId: {0} successfully unsubscribed to webhook", jobId);

            if (unsubscribed == false)
            {
                return HttpStatusCode.InternalServerError;
            }

            await azureTableProvider.DeleteEntityAsync<PageJobEntity>(jobMappingTable, pageJobEntity);
            Trace.TraceInformation("Job with JobId: {0} successfully deleted", jobId);

            return HttpStatusCode.OK;
        }
    }
}