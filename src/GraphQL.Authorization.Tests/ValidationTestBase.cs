using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using GraphQL;
using GraphQL.Execution;
using GraphQL.Http;
using GraphQL.Types;
using GraphQL.Validation;
using Shouldly;
using Xunit;

namespace GraphQL.Authorization.Tests
{
    public class ValidationTestConfig
    {
        private readonly List<IValidationRule> _rules = new List<IValidationRule>();

        public string Query { get; set; }
        public ISchema Schema { get; set; }
        public IEnumerable<IValidationRule> Rules => _rules;
        public ClaimsPrincipal User { get; set; }
        public Inputs Inputs { get; set; }

        public void Rule(params IValidationRule[] rules)
        {
            _rules.AddRange(rules);
        }
    }

    public class GraphQLUserContext : IProvideClaimsPrincipal
    {
        public ClaimsPrincipal User { get; set;}
    }

    public class ValidationTestBase
    {
        private IDocumentExecuter _executor = new DocumentExecuter();
        private IDocumentWriter _writer = new DocumentWriter(indent: true);

        public ValidationTestBase()
        {
            Settings = new AuthorizationSettings();
            Rule = new AuthorizationValidationRule(new AuthorizationEvaluator(Settings));
        }

        protected AuthorizationValidationRule Rule { get; }
        protected AuthorizationSettings Settings { get; }

        protected void ShouldPassRule(Action<ValidationTestConfig> configure)
        {
            var config = new ValidationTestConfig();
            config.Rule(Rule);
            configure(config);

            config.Rules.Any().ShouldBeTrue("Must provide at least one rule to validate against.");

            config.Schema.Initialize();

            var result = Validate(config);

            var message = "";
            if (result.Errors?.Any() == true)
            {
                message = string.Join(", ", result.Errors.Select(x => x.Message));
            }
            result.IsValid.ShouldBeTrue(message);
        }

        protected void ShouldFailRule(Action<ValidationTestConfig> configure)
        {
            var config = new ValidationTestConfig();
            config.Rule(Rule);
            configure(config);

            config.Rules.Any().ShouldBeTrue("Must provide at least one rule to validate against.");

            config.Schema.Initialize();

            var result = Validate(config);

            result.IsValid.ShouldBeFalse("Expected validation errors though there were none.");
        }

        private IValidationResult Validate(ValidationTestConfig config)
        {
            var userContext = new GraphQLUserContext { User = config.User };
            var documentBuilder = new GraphQLDocumentBuilder();
            var document = documentBuilder.Build(config.Query);
            var validator = new DocumentValidator();
            return validator.Validate(config.Query, config.Schema, document, config.Rules, userContext, config.Inputs);
        }

        protected ClaimsPrincipal CreatePrincipal(string authenticationType = null, IDictionary<string, string> claims = null)
        {
            var claimsList = new List<Claim>();

            claims?.Apply(c =>
            {
                claimsList.Add(new Claim(c.Key, c.Value));
            });

            return new ClaimsPrincipal(new ClaimsIdentity(claimsList, authenticationType));
        }
    }
}
