using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Queryable.Models;
using Queryable.Tokenizer;

namespace Queryable.Filters
{
    public class QueryableAttribute : Attribute, IActionFilter, IActionModelConvention
    {
        public string SingularParameterName { get; set; }

        public QueryableAttribute()
        {
        }

        protected string NormalizeFilter(string equation)
        {
            return equation switch
            {
                "eq" => "x.{0} == {1}",
                "nq" => "x.{0} != {1}",
                "lk" => "x.{0}.Contains({1})",
                "nk" => "!x.{0}.Contains({1})",
                "ge" => "x.{0} >= {1}",
                "gt" => "x.{0} > {1}",
                "le" => "x.{0} <= {1}",
                "lt" => "x.{0} < {1}",
                _ => "",
            };
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
            var results = (context.Result as ObjectResult).Value as IQueryable<object>;
            int? resultsCount = 0;
            var count = true;
            bool isSingular = false;

            if (context.HttpContext.Request.Query.TryGetValue("$count", out var _count))
            {
                if (_count.ToString().Trim().ToLower() == "false")
                {
                    count = false;
                }
            }

            try
            {

                if (!string.IsNullOrWhiteSpace(SingularParameterName))
                {
                    if (context.HttpContext.GetRouteData().Values.FirstOrDefault(x => x.Key == SingularParameterName).Value is string value)
                    {
                        isSingular = true;
                        context.Result = new ObjectResult(results.FirstOrDefaultDynamic($"x => x.{SingularParameterName} == {ValueTypeDetector.DetectDynamicType(value)}"));
                        return;

                    }
                }

                if (results == null)
                {
                    context.Result = count ? new ObjectResult(new QueryableResult(0, null)) : new ObjectResult(context.Result);
                }

                if (context.HttpContext.Request.Query.TryGetValue("$filter", out var _filter))
                {
                    if (!string.IsNullOrWhiteSpace(_filter))
                    {
                        StringBuilder linqBuilder = new StringBuilder("x => ");
                        var andParts = _filter.ToString().ToLower().Split(" and ");
                        for (int i = 0; i < andParts.Length; i++)
                        {
                            if (i > 0)
                            {
                                linqBuilder.Append(" && ");
                            }
                            var orParts = andParts[i].Split(" or ");
                            for (int j = 0; j < orParts.Length; j++)
                            {
                                if (j > 0)
                                {
                                    linqBuilder.Append(" || ");
                                }
                                var parts = Regex.Matches(orParts[j].Trim(), @"(?<match>\w+)|\""(?<match>[\w\s]*)""").Cast<Match>().Select(m => m.Groups["match"].Value).ToList();
                                linqBuilder.AppendFormat(NormalizeFilter(parts[1]), parts[0], ValueTypeDetector.DetectDynamicType(parts[2]));
                            }
                        }
                        results = results.WhereDynamic(linqBuilder.ToString());
                    }
                }


                if (context.HttpContext.Request.Query.TryGetValue("$order", out var order))
                {
                    if (!string.IsNullOrWhiteSpace(order))
                    {
                        bool isFirstOrder = true;
                        foreach (var item in ((string)order).Split(","))
                        {
                            var _item = item.Trim();

                            if (!_item.Contains(' '))
                            {
                                _item = "asc " + _item;
                            }
                            var parts = _item.Split(' ');
                            if (isFirstOrder)
                            {
                                isFirstOrder = false;
                                switch (parts[0].Trim().ToLower())
                                {
                                    case "asc": results = (IQueryable<object>)results.OrderByDynamic($"x => x.{parts[1].Trim()}"); break;
                                    case "desc": results = (IQueryable<object>)results.OrderByDescendingDynamic($"x => x.{parts[1].Trim()}"); break;
                                }
                            }
                            else
                            {
                                switch (parts[0].Trim().ToLower())
                                {
                                    case "asc": results = (IQueryable<object>)results.ThenByDynamic($"x => x.{parts[1].Trim()}"); break;
                                    case "desc": results = (IQueryable<object>)results.ThenByDescendingDynamic($"x => x.{parts[1].Trim()}"); break;
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

                if (context.HttpContext.Request.Query.TryGetValue("$singular", out var _singular))
                {
                    if (_singular.ToString().Trim().ToLower() == "true")
                    {
                        context.Result = new ObjectResult(results.FirstOrDefault());
                        return;
                    }
                }

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
                if (isSingular)
                {
                    context.Result = new StatusCodeResult(204);
                }
                else
                {
                    context.Result = count ? new ObjectResult(new QueryableResult(0, null)) : new ObjectResult(null);

                }
            }
        }

        public void OnActionExecuting(ActionExecutingContext context)
        {

        }

        public void Apply(ActionModel action)
        {
            if (!string.IsNullOrWhiteSpace(SingularParameterName))
            {
                var selector = action.Selectors.First();
                var template = selector.AttributeRouteModel.Template;
                if (!template.EndsWith("/") && !string.IsNullOrEmpty(template))
                {
                    template += "/";
                }
                template += $"{{{SingularParameterName}?}}";

                selector.AttributeRouteModel.Template = template;
                action.Selectors.Clear();
                action.Selectors.Add(selector);
            }
        }
    }
}