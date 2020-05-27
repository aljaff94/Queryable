using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Queryable.Models;

namespace Queryable.Filters
{
    public class QueryableAttribute : Attribute, IActionFilter
    {

        public void OnActionExecuted(ActionExecutedContext context)
        {
            var results = (context.Result as ObjectResult).Value as IEnumerable<object>;
            var resultsCount = results?.Count();
            var count = true;

            if (context.HttpContext.Request.Query.TryGetValue("$count", out var _count))
            {
                if (_count.ToString().Trim().ToLower() == "false")
                {
                    count = false;
                }
            }

            try
            {



                if (results == null || resultsCount <= 0)
                {
                    context.Result = count ? new ObjectResult(new QueryableResult(0, null)) : context.Result;
                }

                if (context.HttpContext.Request.Query.TryGetValue("$filter", out var filter))
                {
                    if (!string.IsNullOrWhiteSpace(filter))
                    {
                        foreach (var item in ((string)filter).Split(','))
                        {
                            var parts = Regex.Matches(item.Trim(), @"(?<match>\w+)|\""(?<match>[\w\s]*)""").Cast<Match>().Select(m => m.Groups["match"].Value).ToList();
                            switch (parts[1].Trim().ToLower())
                            {
                                case "eq": results = results.WhereDynamic($"x => x.{parts[0].Trim()} == {parts[2].Trim()}"); break;
                                case "nq": results = results.WhereDynamic($"x => x.{parts[0].Trim()} != {parts[2].Trim()}"); break;
                                case "bt": results = results.WhereDynamic($"x => x.{parts[0].Trim()} > {parts[2].Trim()}"); break;
                                case "be": results = results.WhereDynamic($"x => x.{parts[0].Trim()} >= {parts[2].Trim()}"); break;
                                case "lt": results = results.WhereDynamic($"x => x.{parts[0].Trim()} < {parts[2].Trim()}"); break;
                                case "le": results = results.WhereDynamic($"x => x.{parts[0].Trim()} <= {parts[2].Trim()}"); break;
                                case "lk": results = results.WhereDynamic($"x => x.{parts[0].Trim()}.Contains(\"{parts[2].Trim()}\")"); break;
                                case "nk": results = results.WhereDynamic($"x => !x.{parts[0].Trim()}.Contains(\"{parts[2].Trim()}\")"); break;

                            }
                        }
                    }
                }

                if (context.HttpContext.Request.Query.TryGetValue("$order", out var order))
                {
                    if (!string.IsNullOrWhiteSpace(order))
                    {
                        bool isFirstOrder = true;
                        foreach (var item in ((string)order).Split(','))
                        {
                            var parts = item.Trim().Split(' ');
                            if (isFirstOrder)
                            {
                                isFirstOrder = false;
                                switch (parts[0].Trim().ToLower())
                                {
                                    case "asc": results = results.OrderByDynamic($"x => x.{parts[1].Trim()}"); break;
                                    case "desc": results = results.OrderByDescendingDynamic($"x => x.{parts[1].Trim()}"); break;
                                }
                            }
                            else
                            {
                                switch (parts[0].Trim().ToLower())
                                {
                                    case "asc": results = (IEnumerable<object>)results.ThenByDynamic($"x => x.{parts[1].Trim()}"); break;
                                    case "desc": results = (IEnumerable<object>)results.ThenByDescendingDynamic($"x => x.{parts[1].Trim()}"); break;
                                }
                            }
                        }
                    }
                }

                if (context.HttpContext.Request.Query.TryGetValue("$select", out var select))
                {
                    if (!string.IsNullOrWhiteSpace(select))
                    {
                        StringBuilder sb = new StringBuilder();
                        foreach (var item in ((string)select).Split(','))
                        {
                            var parts = item.Trim().Split(' ');
                            if (parts.Count() == 2)
                            {
                                sb.Append($"{parts[0]} = x.{parts[1]},");
                            }
                            else
                            {
                                sb.Append($"x.{item.Trim()},");
                            }
                        }
                        results = results.SelectDynamic(x => $"new {{{sb.ToString().Substring(0, sb.Length - 1)}}}");
                    }
                }

                resultsCount = results?.Count();

                // skip pages
                int page = 1;
                if (context.HttpContext.Request.Query.TryGetValue("$page", out var pageAsStr))
                {
                    int.TryParse(pageAsStr, out page);
                }

                // take length
                int length = 10;
                if (context.HttpContext.Request.Query.TryGetValue("$length", out var lengthAsStr))
                {
                    int.TryParse(lengthAsStr, out length);
                }


                if (resultsCount > length)
                {
                    var totalSkip = (page - 1) * length;
                    if (totalSkip > 0 && resultsCount > totalSkip)
                    {
                        results = results.Skip(totalSkip);
                    }
                    if (resultsCount > length)
                    {
                        results = results.Take(length);
                    }
                }

                context.Result = count ? new ObjectResult(new QueryableResult(resultsCount.Value, results)) : new ObjectResult(results);
            }
            catch (System.Exception ex)
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(QueryableAttribute));
                logger.LogError(ex, "Error in Queryable equation");
                context.Result = count ? new ObjectResult(new QueryableResult(0, context.Result)) : context.Result;
            }
        }

        public void OnActionExecuting(ActionExecutingContext context)
        {

        }
    }
}