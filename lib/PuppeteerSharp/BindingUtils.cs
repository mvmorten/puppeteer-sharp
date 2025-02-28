using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PuppeteerSharp.Helpers.Json;
using PuppeteerSharp.Messaging;

namespace PuppeteerSharp
{
    internal static class BindingUtils
    {
        internal static string PageBindingInitString(string type, string name)
            => EvaluationString(
                @"function addPageBinding(type, name) {
                    // This is the CDP binding.
                    const callCDP = globalThis[name];

                    // We replace the CDP binding with a Puppeteer binding.
                    Object.assign(globalThis, {
                    [name](...args) {
                      // This is the Puppeteer binding.
                      const callPuppeteer = globalThis[name];
                      callPuppeteer.args ??= new Map();
                      callPuppeteer.callbacks ??= new Map();

                      const seq = (callPuppeteer.lastSeq ?? 0) + 1;
                      callPuppeteer.lastSeq = seq;
                      callPuppeteer.args.set(seq, args);

                      callCDP(
                        JSON.stringify({
                            type,
                            name,
                            seq,
                            args,
                            isTrivial: !args.some(value => {
                            return value instanceof Node;
                            }),
                        })
                      );

                      return new Promise((resolve, reject) => {
                        callPuppeteer.callbacks.set(seq, {
                          resolve(value) {
                            callPuppeteer.args.delete(seq);
                            resolve(value);
                          },
                          reject(value) {
                            callPuppeteer.args.delete(seq);
                            reject(value);
                          },
                        });
                      });
                    },
                  });
                }",
                type,
                name);

        internal static string EvaluationString(string fun, params object[] args)
        {
            return $"({fun})({string.Join(",", args.Select(SerializeArgument))})";

            static string SerializeArgument(object arg)
            {
                return arg == null
                    ? "undefined"
                    : JsonConvert.SerializeObject(arg, JsonHelper.DefaultJsonSerializerSettings);
            }
        }

        internal static async Task ExecuteBindingAsync(ExecutionContext context, BindingCalledResponse e, ConcurrentDictionary<string, Binding> pageBindings)
        {
            var binding = pageBindings[e.BindingPayload.Name];
            var methodParams = binding.Function.Method.GetParameters().Select(parameter => parameter.ParameterType).ToArray();

            var args = e.BindingPayload.Args.Select((token, i) => token.ToObject(methodParams[i])).Cast<object>().ToArray();

            await binding.RunAsync(context, e.BindingPayload.Seq, args, e.BindingPayload.IsTrivial).ConfigureAwait(false);
        }

        internal static async Task<object> ExecuteBindingAsync(Delegate binding, object[] rawArgs)
        {
            const string taskResultPropertyName = "Result";
            object result;
            var methodParams = binding.Method.GetParameters().Select(parameter => parameter.ParameterType).ToArray();

            var args = rawArgs.Select((arg, i) => arg is JToken token ? token.ToObject(methodParams[i]) : arg).ToArray();

            result = binding.DynamicInvoke(args);
            if (result is Task taskResult)
            {
                await taskResult.ConfigureAwait(false);

                if (taskResult.GetType().IsGenericType)
                {
                    // the task is already awaited and therefore the call to property Result will not deadlock
                    result = taskResult.GetType().GetProperty(taskResultPropertyName).GetValue(taskResult);
                }
            }

            return result;
        }
    }
}
