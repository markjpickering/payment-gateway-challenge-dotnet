global using AwesomeAssertions;

global using NSubstitute;

global using PaymentGateway.Api.Security;
global using PaymentGateway.Api.Tests.TestSupport;
global using PaymentGateway.ApiInterface.Dtos.Requests;
global using PaymentGateway.ApiInterface.Dtos.Responses;
global using PaymentGateway.BusinessLogic.Mapping;
global using PaymentGateway.BusinessLogic.Validation;
global using PaymentGateway.DataInterface;
global using PaymentGateway.DataInterface.Dtos.Requests;
global using PaymentGateway.DataInterface.Dtos.Responses;
global using PaymentGateway.DataInterface.Entities;
global using PaymentGateway.DataInterface.Enums;
global using PaymentGateway.DataLayer.Bank;
global using PaymentGateway.Shared.Outcomes;

global using Xunit;