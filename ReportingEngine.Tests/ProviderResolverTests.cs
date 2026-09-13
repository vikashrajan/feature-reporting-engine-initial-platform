using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ReportingEngine.Application.Abstractions.Data;
using ReportingEngine.Application.Abstractions.Delivery;
using ReportingEngine.Application.Abstractions.Files;
using ReportingEngine.Application.Options;
using ReportingEngine.Infrastructure.Data;
using ReportingEngine.Infrastructure.Delivery;
using ReportingEngine.Infrastructure.Files;

namespace ReportingEngine.Tests;

public class ProviderResolverTests
{
    [Fact]
    public void FileGeneratorResolver_ShouldResolveSupportedFormats()
    {
        // Arrange
        var generators = new IFileGenerator[]
        {
            new CsvFileGenerator(),
            new JsonFileGenerator(),
            new TxtFileGenerator()
        };
        var resolver = new FileGeneratorResolver(generators);

        // Act & Assert
        resolver.IsSupported("CSV").Should().BeTrue();
        resolver.IsSupported("JSON").Should().BeTrue();
        resolver.IsSupported("TXT").Should().BeTrue();
        resolver.IsSupported("EXCEL").Should().BeFalse();

        resolver.Resolve("CSV").Should().BeOfType<CsvFileGenerator>();
        resolver.Resolve("JSON").Should().BeOfType<JsonFileGenerator>();
        resolver.Resolve("TXT").Should().BeOfType<TxtFileGenerator>();

        Action act = () => resolver.Resolve("UNSUPPORTED");
        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void DeliveryProviderResolver_ShouldResolveSupportedDeliveryTypes()
    {
        // Arrange
        var emailOptions = Options.Create(new EmailOptions { FromAddress = "noreply@example.com" });
        var emailLogger = new Mock<ILogger<EmailDeliveryProvider>>().Object;
        var folderLogger = new Mock<ILogger<SharedFolderDeliveryProvider>>().Object;
        var sftpLogger = new Mock<ILogger<SftpDeliveryProvider>>().Object;
        var azureLogger = new Mock<ILogger<AzureFileShareDeliveryProvider>>().Object;
        var s3Logger = new Mock<ILogger<S3DeliveryProvider>>().Object;

        var providers = new IDeliveryProvider[]
        {
            new EmailDeliveryProvider(emailOptions, emailLogger),
            new SharedFolderDeliveryProvider(folderLogger),
            new SftpDeliveryProvider(sftpLogger),
            new BlobDeliveryProvider(azureLogger),
            new AzureFileShareDeliveryProvider(azureLogger),
            new S3DeliveryProvider(s3Logger)
        };

        var resolver = new DeliveryProviderResolver(providers);

        // Act & Assert
        resolver.IsSupported("EMAIL").Should().BeTrue();
        resolver.IsSupported("SHARED_FOLDER").Should().BeTrue();
        resolver.IsSupported("SFTP").Should().BeTrue();
        resolver.IsSupported("AZURE_FILE_SHARE").Should().BeTrue();
        resolver.IsSupported("BLOB").Should().BeTrue();
        resolver.IsSupported("S3").Should().BeTrue();

        resolver.Resolve("EMAIL").Should().BeOfType<EmailDeliveryProvider>();
        resolver.Resolve("SHARED_FOLDER").Should().BeOfType<SharedFolderDeliveryProvider>();
        resolver.Resolve("SFTP").Should().BeOfType<SftpDeliveryProvider>();
        resolver.Resolve("BLOB").Should().BeOfType<BlobDeliveryProvider>();
        resolver.Resolve("AZURE_FILE_SHARE").Should().BeOfType<AzureFileShareDeliveryProvider>();
        resolver.Resolve("S3").Should().BeOfType<S3DeliveryProvider>();

        Action act = () => resolver.Resolve("NON_EXISTENT");
        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void DataSourceProviderResolver_ShouldResolveSqlAndCosmos()
    {
        // Arrange
        var connResolver = new Mock<IConnectionStringResolver>();
        var execOptions = Options.Create(new ExecutionOptions());

        var providers = new IDataSourceProvider[]
        {
            new SqlDataSourceProvider(connResolver.Object, execOptions),
            new CosmosDataSourceProvider(connResolver.Object)
        };

        var resolver = new DataSourceProviderResolver(providers);

        // Act & Assert
        resolver.IsSupported("SQL").Should().BeTrue();
        resolver.IsSupported("COSMOS").Should().BeTrue();
        resolver.IsSupported("POSTGRES").Should().BeFalse();

        resolver.Resolve("SQL").Should().BeOfType<SqlDataSourceProvider>();
        resolver.Resolve("COSMOS").Should().BeOfType<CosmosDataSourceProvider>();

        Action act = () => resolver.Resolve("ORACLE");
        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void ConnectionStringResolver_ShouldResolveConfiguredReference()
    {
        var options = new Mock<IOptionsMonitor<ConnectionReferencesOptions>>();
        options.SetupGet(x => x.CurrentValue).Returns(new ConnectionReferencesOptions
        {
            Values =
            {
                ["DemoDB"] = "Server=(localdb)\\mssqllocaldb;Database=DemoDB;"
            }
        });
        var resolver = new ConnectionStringResolver(options.Object);

        var connectionString = resolver.Resolve("DemoDB");

        connectionString.Should().Contain("Database=DemoDB");
    }

    [Fact]
    public void ConnectionStringResolver_ShouldResolveNestedEnvironmentReference()
    {
        const string envKey = "ConnectionReferences__Values__DemoDB_ENV_TEST";
        Environment.SetEnvironmentVariable(envKey, "Server=env;Database=DemoDB;");

        try
        {
            var options = new Mock<IOptionsMonitor<ConnectionReferencesOptions>>();
            options.SetupGet(x => x.CurrentValue).Returns(new ConnectionReferencesOptions());
            var resolver = new ConnectionStringResolver(options.Object);

            var connectionString = resolver.Resolve("DemoDB_ENV_TEST");

            connectionString.Should().Contain("Server=env");
        }
        finally
        {
            Environment.SetEnvironmentVariable(envKey, null);
        }
    }

    [Fact]
    public void ConnectionStringResolver_ShouldAcceptRawConnectionString()
    {
        var options = new Mock<IOptionsMonitor<ConnectionReferencesOptions>>();
        options.SetupGet(x => x.CurrentValue).Returns(new ConnectionReferencesOptions());
        var resolver = new ConnectionStringResolver(options.Object);

        var connectionString = resolver.Resolve("Server=(localdb)\\mssqllocaldb;Database=demo;");

        connectionString.Should().Contain("Database=demo");
    }

    [Fact]
    public void EmailConnectionConfig_FromSecretReference_ShouldOverrideDefaultSmtpSettings()
    {
        var options = new EmailOptions
        {
            Host = "smtp.default.local",
            Port = 25,
            FromAddress = "default@example.com",
            FromDisplayName = "Default",
            SmtpTimeoutSeconds = 120
        };
        var secret = """
            {
              "Host": "smtp.gmail.com",
              "Port": 587,
              "EnableSsl": true,
              "FromAddress": "reports@example.com",
              "FromDisplayName": "Reports",
              "UserName": "reports@example.com",
              "Password": "secret",
              "SmtpTimeoutSeconds": 180
            }
            """;

        var config = EmailConnectionConfig.From(options, secret);

        config.Host.Should().Be("smtp.gmail.com");
        config.Port.Should().Be(587);
        config.EnableSsl.Should().BeTrue();
        config.FromAddress.Should().Be("reports@example.com");
        config.TimeoutSeconds.Should().Be(180);
    }

    [Fact]
    public void AzureFileShareConnectionConfig_Parse_ShouldReadShareAndDirectoryFromJson()
    {
        var secret = """
            {
              "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=test;AccountKey=secret;EndpointSuffix=core.windows.net",
              "ShareName": "childfolder",
              "DirectoryPath": "reports/{ReportCode}"
            }
            """;
        var tokens = new DeliveryTokenContext("CUST", "DAILY", DateTime.UtcNow, 10, 1);

        var config = AzureFileShareConnectionConfig.Parse(null, secret, tokens);

        config.ConnectionString.Should().Contain("AccountName=test");
        config.ShareName.Should().Be("childfolder");
        config.DirectoryPath.Should().Be("reports/DAILY");
    }

    [Fact]
    public void AzureFileShareConnectionConfig_Parse_ShouldSupportLegacyBlobDestinationAsShareName()
    {
        var secret = "DefaultEndpointsProtocol=https;AccountName=test;AccountKey=secret;EndpointSuffix=core.windows.net";
        var tokens = new DeliveryTokenContext("CUST", "DAILY", DateTime.UtcNow, 10, 1);

        var config = AzureFileShareConnectionConfig.Parse("childfolder", secret, tokens);

        config.ConnectionString.Should().Contain("AccountName=test");
        config.ShareName.Should().Be("childfolder");
        config.DirectoryPath.Should().BeEmpty();
    }

    [Fact]
    public void S3ConnectionConfig_Parse_ShouldReadBucketAndPrefixFromJson()
    {
        var secret = """
            {
              "AccessKeyId": "access",
              "SecretAccessKey": "secret",
              "Region": "ap-south-1",
              "BucketName": "reports-bucket",
              "Prefix": "exports/{ReportCode}"
            }
            """;
        var tokens = new DeliveryTokenContext("CUST", "DAILY", DateTime.UtcNow, 10, 1);

        var config = S3ConnectionConfig.Parse(null, secret, tokens);

        config.AccessKeyId.Should().Be("access");
        config.SecretAccessKey.Should().Be("secret");
        config.Region.Should().Be("ap-south-1");
        config.BucketName.Should().Be("reports-bucket");
        config.Prefix.Should().Be("exports/DAILY");
    }

    [Fact]
    public void S3ConnectionConfig_Parse_ShouldSupportS3UriDestination()
    {
        var tokens = new DeliveryTokenContext("CUST", "DAILY", DateTime.UtcNow, 10, 1);

        var config = S3ConnectionConfig.Parse("s3://reports-bucket/exports/{ReportCode}", null, tokens);

        config.BucketName.Should().Be("reports-bucket");
        config.Prefix.Should().Be("exports/DAILY");
    }
}
