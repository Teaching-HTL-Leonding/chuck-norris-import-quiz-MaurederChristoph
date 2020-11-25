using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

HttpClient client = new HttpClient();
int maxNewJokes = 0;
var factory = new ChuckJokeContextFactory();
using var dbContext = factory.CreateDbContext();
int numberOfJokes = 0;

try {
    if (args[0].Equals("clear")) {
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM ChuckJokes");
    }
    numberOfJokes = Convert.ToInt32(args[0]);
} catch (IndexOutOfRangeException) {
    numberOfJokes = 5;
} catch (FormatException) {
    Console.WriteLine("Invalid Argument");
}
using var transaction = await dbContext.Database.BeginTransactionAsync();
try {
    if (numberOfJokes > 10) {
        Console.WriteLine("The number is too large");
        numberOfJokes = 0;
    }

    for (int i = 0; i < numberOfJokes; i++) {
        await getJokeAsync(client);
    }
    await transaction.CommitAsync();
} catch (SqlException ex) {
    Console.WriteLine(ex);
}
async Task getJokeAsync(HttpClient client) {
    string responseBody = "";
    try {
        HttpResponseMessage response = await client.GetAsync("https://api.chucknorris.io/jokes/random");
        response.EnsureSuccessStatusCode();
        responseBody = await response.Content.ReadAsStringAsync();
    } catch (HttpRequestException e) {
        Console.WriteLine("\nException Caught!");
        Console.WriteLine("Message :{0} ", e.Message);
    }
    await writeToDBAsync(responseBody);
}
async Task writeToDBAsync(string responseBody) {
    using var dbContext = factory!.CreateDbContext();
    var options = new JsonSerializerOptions() {
        IncludeFields = true,
    };
    var jsonContext = JsonSerializer.Deserialize<JsonContext>(responseBody, options);
    var joke = new ChuckJoke { ChuckNorrisId = jsonContext!.id, Url = jsonContext.url, Joke = jsonContext.value };
    if (dbContext.ChuckJokes.Contains(joke) && maxNewJokes <= 10) {
        await getJokeAsync(client);
        maxNewJokes++;
        Console.WriteLine("Joke already exist. Generate new one");
    }
    maxNewJokes = 0;
    dbContext.ChuckJokes.Add(joke);
    await dbContext.SaveChangesAsync();
}
public class JsonContext {
    public string[] categories = { };
    public string created_at = "";
    public string icon_url = "";
    public string id = "";
    public string updated_at = "";
    public string url = "";
    public string value = "";
}
class ChuckJoke {
    public int Id { get; set; }

    [MaxLength(40)]
    public string ChuckNorrisId { get; set; } = string.Empty;

    [MaxLength(1024)]
    public string Url { get; set; } = string.Empty;

    public string Joke { get; set; } = string.Empty;
}
class ChuckNorrisContext : DbContext {
    public DbSet<ChuckJoke> ChuckJokes { get; set; }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public ChuckNorrisContext(DbContextOptions<ChuckNorrisContext> options)
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        : base(options) {
    }
}
class ChuckJokeContextFactory : IDesignTimeDbContextFactory<ChuckNorrisContext> {
    public ChuckNorrisContext CreateDbContext(string[]? args = null) {
        var configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();

        var optionsBuilder = new DbContextOptionsBuilder<ChuckNorrisContext>();
        optionsBuilder
            // Uncomment the following line if you want to print generated
            // SQL statements on the console.
            .UseLoggerFactory(LoggerFactory.Create(builder => builder.AddConsole()))
            .UseSqlServer(configuration["ConnectionStrings:DefaultConnection"]);

        return new ChuckNorrisContext(optionsBuilder.Options);
    }
}
