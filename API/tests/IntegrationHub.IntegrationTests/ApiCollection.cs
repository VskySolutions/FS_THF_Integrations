namespace IntegrationHub.IntegrationTests;

/// <summary>
/// Shares a single <see cref="IntegrationHubApiFactory"/> (one app boot, one bootstrap
/// seeder) across all integration test classes and runs them sequentially, avoiding a
/// seeder race on the unique tenant/user keys.
/// </summary>
[CollectionDefinition("Api")]
public sealed class ApiCollection : ICollectionFixture<IntegrationHubApiFactory>;
