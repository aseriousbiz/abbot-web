# Playbook Troubleshooting Guide

This document provides a list of troubleshooting guides and tips for dealing with issues that arise from production relating to Playbooks.

## Playbook Run Logs

If you know the playbook run that is having issues, or just want to get more information about a playbook run, you can get all the logs for a given playbook run with a fairly simple Kusto query.
Take the Run ID (looks like `aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee`) and go to [abbot-insights](https://portal.azure.com/#@aseriousbusiness.com/resource/subscriptions/114d4132-6977-430c-a803-38afcadd0e8b/resourceGroups/abbot-production/providers/microsoft.insights/components/abbot-insights/logs)

```kql
union traces, exception
| extend BusCorrelationId = tostring(Properties.BusCorrelationId)
| where BusCorrelationId == "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"
| order by TimeGenerated asc
```

You can also get an overview of all the activity in a given playbook run by using the [end-to-end transaction search](https://portal.azure.com/#@aseriousbusiness.com/resource/subscriptions/114d4132-6977-430c-a803-38afcadd0e8b/resourceGroups/abbot-production/providers/microsoft.insights/components/abbot-insights/searchV1) and searching for the Run ID.
You will see entries for **both** the run execution itself and for any pages referencing the run, so make sure you look for a "REQUEST" entry with a name like `playbook-runner-v2 receive` or `step-runner-v2 receive`

## Slow Execution of Playbooks

If playbooks are taking a long time to move from step to step, we should check the depth of the [`playbook-runner-v2`](https://portal.azure.com/#@aseriousbusiness.com/resource/subscriptions/114d4132-6977-430c-a803-38afcadd0e8b/resourceGroups/abbot-production/providers/Microsoft.ServiceBus/namespaces/abbot-production-bus53eb4bc1/queues/playbook-runner-v2/overview) and [`step-runner-v2`](https://portal.azure.com/#@aseriousbusiness.com/resource/subscriptions/114d4132-6977-430c-a803-38afcadd0e8b/resourceGroups/abbot-production/providers/Microsoft.ServiceBus/namespaces/abbot-production-bus53eb4bc1/queues/step-runner-v2/overview) queues in Azure Service Bus.

If those queues are deep, it indicates the that the consumers (`PlaybookRunStateMachine` and `StepRunnerConsumer` respectively) are taking too long to process messages.
