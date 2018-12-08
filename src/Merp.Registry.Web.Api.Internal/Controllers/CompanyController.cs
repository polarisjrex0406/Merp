﻿using System;
using System.Threading.Tasks;
using Acl.RegistryResolutionServices;
using Merp.Registry.Web.Api.Internal.Models.Company;
using Merp.Registry.Web.Api.Internal.WorkerServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Merp.Registry.Web.Api.Internal.Controllers
{
    [Authorize]
    public class CompanyController : ControllerBase
    {
        public CompanyControllerWorkerServices WorkerServices { get; private set; }

        public Resolver ResolverServiceProxy { get; private set; }

        public CompanyController(CompanyControllerWorkerServices workerServices, Resolver resolverServiceProxy)
        {
            WorkerServices = workerServices ?? throw new ArgumentNullException(nameof(workerServices));
            ResolverServiceProxy = resolverServiceProxy ?? throw new ArgumentNullException(nameof(resolverServiceProxy));
        }

        [HttpPost]
        public async Task<IActionResult> Register([FromBody]RegisterModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            await WorkerServices.RegisterCompanyAsync(model);
            return Ok();
        }

        [HttpGet]
        public async Task<IActionResult> LookupByVatNumber(string vatNumber, string countryCode)
        {
            if (string.IsNullOrWhiteSpace(vatNumber) || string.IsNullOrWhiteSpace(countryCode))
            {
                return BadRequest();
            }

            try
            {
                var personInformation = await ResolverServiceProxy.LookupCompanyInfoByVatNumberAsync(countryCode, vatNumber);
                if (personInformation == null)
                {
                    return NotFound();
                }

                return Ok(personInformation);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }
    }
}