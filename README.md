
# Appy.GitDb

<a href="https://ci.appveyor.com/project/yellowlineparking/ylp-gitdb">
  <img src="https://ci.appveyor.com/api/projects/status/github/YellowLineParking/Appy.GitDb?branch=master&svg=true"  />
</a>


Appy.GitDb is a set of packages and applications to use Git as a NoSql database

## Getting Started

GitDb can be accessd in two different modes:

- Local: In this mode, you access a local git repository directly
- Remote: In this mode, you install git server on a node and access it through a REST API

### Local mode

To start using GitDb locally, first install the `Appy.GitDb.Local` NuGet package:

- Package Manager Console
```csharp
Install-Package Appy.GitDb.Local
```

or 

- .NET CLI Console
```
dotnet add package CsvHelper
```


Now you can use a local repository as a Git database:

```csharp
// 1. Instantiate a new instance of the local git database
IGitDb db = new LocalGitDb(@"c:\path\to\a\repository");

// 2. Save an object to the database
var myObject = new SomeClass
{
  SomeProperty = "SomeValue"
};

await db.Save("master", "commit message", new Document<SomeClass> {Key = "key", Value = myObect}, new Author("name", "mail@mail.com"));

// 3. Retrieve the object
var theObject = await db.Get<SomeClass>("master", "key")
```

### Remote mode

In order to use Git as a remote database, you need to install the `Appy.GitDb.Server` project on a server.

Once the server is installed, you can use the `Appy.GitDb.Remote` package to talk to the server:

```
Install-Package Appy.GitDb.Remote
```

```csharp
// 1. Instantiate a new instance of the remote git database
IGitDb db = new RemoteGitDb("username", "password", "http://url-of-git-database.com");

// 2. Save an object to the database
var myObject = new SomeClass
{
  SomeProperty = "SomeValue"
};

db.Save("master", "commit message", new Document<SomeClass> {Key = "key", Value = myObect}, new Author("name", "mail@mail.com"));

// 3. Retrieve the object
var theObject = db.Get<SomeClass>("master", "key")

```

Both the local as well as the remote git db implement the same interface, so they're easily interchangeable. The interface is defined as follows:

```csharp
public interface IGitDb : IDisposable
{
    Task<string> Get(string branch, string key);
    Task<T> Get<T>(string branch, string key) where T : class;

    Task<IReadOnlyCollection<T>> GetFiles<T>(string branch, string key);
    Task<IReadOnlyCollection<string>> GetFiles(string branch, string key);

    Task<string> Save(string branch, string message, Document document, Author author);
    Task<string> Save<T>(string branch, string message, Document<T> document, Author author);

    Task<string> Delete(string branch, string key, string message, Author author);

    Task Tag(Reference reference);
    Task DeleteTag(string tag);
    Task CreateBranch(Reference reference);
    Task<IEnumerable<string>> GetAllBranches();
    Task<ITransaction> CreateTransaction(string branch);
    Task CloseTransactions(string branch);

    Task<string> MergeBranch(string source, string target, Author author, string message);
    Task DeleteBranch(string branch);

    Task<Diff> Diff(string reference, string reference2);
}
```
## Transactions

In order to add, update or remove multiple files in a single commit, you need to create a transaction. You can do so by creating a transaction from the database:

```csharp
IGitDb db = // new LocalGitDb(...) or new RemoteGitDb(...)

using(var transaction = await db.CreateTransaction("master"))
{
  await transaction.Add(document);
  await transaction.Delete(key);
  await transaction.AddMany(documentList);
  ...
  ...

  await transaction.Commit("commit message", new Author("name", "mail@mail.com"));
  // or
  await transaction.Abort();
}
```

The transaction returned from the `CreateTransaction`-method implements the following interface (in local as well as in remote mode):

```csharp
public interface ITransaction : IDisposable
{
    Task Add(Document document);
    Task Add<T>(Document<T> document);
    
    Task AddMany<T>(IEnumerable<Document<T>> documents);
    Task AddMany(IEnumerable<Document> documents);
    
    Task Delete(string key);
    Task DeleteMany(IEnumerable<string> keys);
    
    Task<string> Commit(string message, Author author);
    Task Abort();
}
```

## Branch management
Currently, GitDb supports the following methods for managing branches:

```csharp
// Creates a tag at the specified reference (commit, branch or other tag)
Task Tag(Reference reference);

// Deletes a tag with the specified name
Task DeleteTag(string tag);

// Creates a branch at the specified reference (commit, branch or tag)
Task CreateBranch(Reference reference);

// Gets a list of all branches
Task<IEnumerable<string>> GetAllBranches();

// Merges a branch into another branch
// Currently this method does the equivalent of the following steps:
// - Does a soft reset on the source branch
// - Switches to the target branch
// - Creates a single commit using the provided author and commit message
// It effectively does a squash and rebase of the source branch on top of the target branch
Task<string> MergeBranch(string source, string target, Author author, string message);

// Removes a branch
Task DeleteBranch(string branch);
```

## Indexing changes
In order to use GitDb as source for reading data, you can use the aforementioned methods to read the documents by key. For improved reading performance and more query options, it can be useful however to write the data to a denormalized store. 

To aid with this process, there's a helper library `Appy.GitDb.Watcher` provided. 
This package will monitor a git repository and notify any changes so they can be indexed.

First, install the package:

```
Install-Package Appy.GitDb.Watcher
```

Now, we can set up the watcher to check for changes:

```csharp
new Watcher(
  path: @"path\to\local\repository",
  interval: intervalToPollForChangesInMilliseconds,
  branchAdded: branchAdded => {},
  branchChanged: branchChanged => {},
  branchRemoved: branchRemoved => {}
)
```
The three methods will be called whenever the watcher detects any changes in the repository. They will be provided with the following arguments:

```csharp
public class BranchAdded
{
  // Newly created branch
  public BranchInfo Branch { get; set; }
  
  // Branch where this branch was created from
  public string BaseBranch { get; set; }
  
  // Items that were added in comparison with the base branch
  public List<ItemAdded> Added { get; set; } = new List<ItemAdded>();
  
  // Items that were modified in comparison with the base branch
  public List<ItemModified> Modified { get; set; } = new List<ItemModified>();
  
  // Items that were delete in comparison with the base branch
  public List<ItemDeleted> Deleted { get; set; } = new List<ItemDeleted>();
  
  // Items that were renamed in comparison with the base branch
  public List<ItemRenamed> Renamed { get; set; } = new List<ItemRenamed>();
}

public class BranchChanged
{
  // Branch that was changed
  public BranchInfo Branch { get; set; }
  
  // Sha of the previous commit this branch was at
  public string PreviousCommit { get; set; }
  
  // Items that were added in comparison with the previous commit
  public List<ItemAdded> Added { get; set; } = new List<ItemAdded>();
  
  // Items that were modified in comparison with the previous commit
  public List<ItemModified> Modified { get; set; } = new List<ItemModified>();
  
  // Items that were delete in comparison with the previous commit
  public List<ItemDeleted> Deleted { get; set; } = new List<ItemDeleted>();
  
  // Items that were renamed in comparison with the previous commit
  public List<ItemRenamed> Renamed { get; set; } = new List<ItemRenamed>();
}

public class BranchRemoved
{
  // Name of the branch that was removed
  public BranchInfo Branch { get; set; }
}
    
```

You can use these events to obtain the data that was changed and store them in a read-optimized database (denormalized SQL, Azure Table Storage, RavenDb, Couchbase, Elasticsearch, ...)

## Building the solution

Execute the following command:

```
build.bat dev
```

This will execute the tasks `clean`, `compile`, `test`, `pack`
You can execute any of these tasks separately by running `build <task>`

