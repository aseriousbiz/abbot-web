# Azure Service Bus

We use MassTransit to provide an Event Bus that allows distributing events throughout the system.
Since we only have a single Web service, this is mostly about designing the code and supporting future scale-up.
Despite only a single service, we support running the application with an Azure Service Bus backend.

## Creating an Azure Service Bus

1. Create a Service Bus in the Azure Portal, if necessary.
2. Ensure the identity that the application runs under has the "Azure Service Bus Data Owner" role on the Service Bus
   * When running locally, log in to your Azure account using `az login` and make sure your user has this permission
   * When running in a deployed environment, use Azure Managed Identity

## Pointing Abbot.Web at the service bus

1. Set the `Eventing:Transport` setting to `AzureServiceBus`
2. Set the `Eventing:Endpoint` setting to `sb://[service-bus-name].servicebus.windows.net/`
