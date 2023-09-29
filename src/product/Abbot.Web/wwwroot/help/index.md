# Help

If you're having a bot emergency, contact us at <a href="mailto:help@ab.bot">help@ab.bot</a>.

## Getting Started
The easiest way to get started with Abbot is by following our [Getting Started](guides) guide.

For a more concise, reference-based view; please refer to [Abbot's reference documentation](reference).

## Frequently Asked Questions

### What languages are supported?
You can write skills using C# 9, Python 3.7, or JavaScript in Node 10.
Later we will support WASM assemblies in a paid version of Abbot.

### What is the right way to save data from my script?
Abbot provides a convenient way to save data from your skills. Please take a look at [Abbot's reference documentation](reference) for details on how to use Abbot's brain.

### How do skills deal with sensitive data?
There is a Secret management system built in to Abbot. This allows your script to work with sensitive data without including in your script. Please note that developers can still get access to the data since we provide a scripting environment; so you should never store passwords in Secrets (and you should be careful about who you grant access to Abbot). The easiest way to see how to access Secrets in an Abbot skill is via [Abbot's reference documentation](reference).

### What Libraries are available in my skills?

We provide a set of pre-installed libraries for you to use in your skills.
Custom dependencies will be available later in a paid version of Abbot.

#### Preloaded C# libraries
  * All libraries available in .NET Core 3.1
  * NodaTime 3.0.3
  * NewtonSoft.Json 12.0.3
  * HtmlAgilityPack 1.11.28

#### Preloaded JavaScript libraries
  * Lodash 4.17.19
  * Axios 0.19.2

#### Preloaded Python libraries
   * Requests 2.24.0
   * Pandas 1.0.5
   * Numpy 1.19.4
   * BeautifulSoup 4.9.3
   * Soupsieve 2.0.1

### I'm experiencing an issue, and would like to report it!
First, we're sorry that you're running into issues. Please take a look at our list of [known issues](knownissues) to make sure we're not already aware of it. If not, please drop us a line at <a href="mailto:help@ab.bot">help@ab.bot</a> or send us feedback directly using `@abbot feedback` from chat or the Bot Console.
