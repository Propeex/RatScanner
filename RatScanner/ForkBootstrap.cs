using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RatScanner.TarkovDev.GraphQL;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace RatScanner;

/// <summary>
/// Fork-specific startup behavior.
///
/// The fork is distributed exclusively through GitHub Releases, so the official
/// RatScanner updater is disabled. Release builds also include a sanitized item
/// snapshot from json.tarkov.dev so the application can start while the GraphQL
/// endpoint is unavailable.
/// </summary>
internal static class ForkBootstrap {
	private const string ValidationEnvironmentVariable = "RATSCANNER_VALIDATE_FALLBACK";

	private static readonly JsonSerializerSettings JsonSettings = new() {
		MissingMemberHandling = MissingMemberHandling.Ignore,
		NullValueHandling = NullValueHandling.Ignore,
		TypeNameHandling = TypeNameHandling.Auto,
		TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
	};

	[ModuleInitializer]
	internal static void Initialize() {
		bool fallbackPrepared = false;

		try {
			DisableOfficialUpdater();
			fallbackPrepared = PrepareOfflineCaches();
		} catch {
			// Startup must not fail because the fork bootstrap itself encountered an
			// unexpected problem. The regular startup path will provide the normal log.
		}

		if (Environment.GetEnvironmentVariable(ValidationEnvironmentVariable) == "1") {
			Environment.Exit(fallbackPrepared ? 0 : 20);
		}
	}

	private static void DisableOfficialUpdater() {
		FieldInfo? cacheField = typeof(ApiManager).GetField("ResCache", BindingFlags.NonPublic | BindingFlags.Static);
		if (cacheField?.GetValue(null) is not Dictionary<ApiManager.ResourceType, string> resources) return;

		resources[ApiManager.ResourceType.ClientVersion] = RatConfig.Version;
		resources[ApiManager.ResourceType.ClientForceUpdateVersions] = string.Empty;
		resources[ApiManager.ResourceType.UpdaterLink] = string.Empty;
	}

	private static bool PrepareOfflineCaches() {
		string fallbackDirectory = Path.Combine(RatConfig.Paths.Data, "api-fallback");
		string regularItemsPath = Path.Combine(fallbackDirectory, "regular-items.json");
		string regularLocalePath = Path.Combine(fallbackDirectory, "regular-items-en.json");
		string pveItemsPath = Path.Combine(fallbackDirectory, "pve-items.json");
		string pveLocalePath = Path.Combine(fallbackDirectory, "pve-items-en.json");

		if (!File.Exists(regularItemsPath) || !File.Exists(regularLocalePath) ||
			!File.Exists(pveItemsPath) || !File.Exists(pveLocalePath)) return false;

		string regularCache = BuildItemCache(regularItemsPath, regularLocalePath);
		string pveCache = BuildItemCache(pveItemsPath, pveLocalePath);

		if (!ValidateItemCache(regularCache) || !ValidateItemCache(pveCache)) return false;

		foreach (LanguageCode language in Enum.GetValues<LanguageCode>()) {
			foreach (GameMode gameMode in Enum.GetValues<GameMode>()) {
				string itemCache = gameMode == GameMode.Pve ? pveCache : regularCache;
				RatConfig.WriteToCache($"items_{language}_{gameMode}", itemCache);
				EnsureEmptyCache($"tasks_{language}_{gameMode}");
				EnsureEmptyCache($"hideout_{language}_{gameMode}");
				EnsureEmptyCache($"maps_{language}_{gameMode}");
			}
		}

		return true;
	}

	private static void EnsureEmptyCache(string key) {
		if (RatConfig.ReadFromCache(key, out string existing) && !string.IsNullOrWhiteSpace(existing)) return;
		RatConfig.WriteToCache(key, "{\"data\":{\"data\":[]}}");
	}

	private static string BuildItemCache(string dataPath, string localePath) {
		JObject source = JObject.Parse(File.ReadAllText(dataPath));
		JObject localeDocument = JObject.Parse(File.ReadAllText(localePath));
		JObject locale = localeDocument["data"] as JObject ?? new JObject();
		JObject items = source["data"]?["items"] as JObject ?? throw new InvalidDataException("Fallback item snapshot does not contain data.items.");

		JArray sanitizedItems = new();
		foreach (JProperty itemProperty in items.Properties()) {
			if (itemProperty.Value is not JObject item) continue;

			JObject sanitized = new() {
				["id"] = item["id"]?.DeepClone(),
				["name"] = Translate(item["name"], locale),
				["normalizedName"] = item["normalizedName"]?.DeepClone(),
				["shortName"] = Translate(item["shortName"], locale),
				["description"] = Translate(item["description"], locale),
				["basePrice"] = item["basePrice"]?.DeepClone() ?? 0,
				["updated"] = item["updated"]?.DeepClone(),
				["width"] = item["width"]?.DeepClone() ?? 1,
				["height"] = item["height"]?.DeepClone() ?? 1,
				["backgroundColor"] = item["backgroundColor"]?.DeepClone() ?? "blue",
				["iconLink"] = item["iconLink"]?.DeepClone(),
				["gridImageLink"] = item["gridImageLink"]?.DeepClone(),
				["baseImageLink"] = item["baseImageLink"]?.DeepClone(),
				["inspectImageLink"] = item["inspectImageLink"]?.DeepClone(),
				["image512pxLink"] = item["image512pxLink"]?.DeepClone(),
				["image8xLink"] = item["image8xLink"]?.DeepClone(),
				["wikiLink"] = item["wikiLink"]?.DeepClone(),
				["types"] = item["types"]?.DeepClone() ?? new JArray(),
				["avg24hPrice"] = item["avg24hPrice"]?.DeepClone(),
				["lastLowPrice"] = item["lastLowPrice"]?.DeepClone(),
				["changeLast48h"] = item["changeLast48h"]?.DeepClone(),
				["changeLast48hPercent"] = item["changeLast48hPercent"]?.DeepClone(),
				["low24hPrice"] = item["low24hPrice"]?.DeepClone(),
				["high24hPrice"] = item["high24hPrice"]?.DeepClone(),
				["lastOfferCount"] = item["lastOfferCount"]?.DeepClone(),
				["weight"] = item["weight"]?.DeepClone(),
				["velocity"] = item["velocity"]?.DeepClone(),
				["minLevelForFlea"] = item["minLevelForFlea"]?.DeepClone(),
				["hasGrid"] = item["hasGrid"]?.DeepClone(),
				["link"] = item["link"]?.DeepClone(),
				["conflictingSlotIds"] = item["conflictingSlotIds"]?.DeepClone() ?? new JArray(),
				["sellFor"] = new JArray(),
				["buyFor"] = new JArray(),
				["containsItems"] = new JArray(),
				["categories"] = new JArray(),
				["handbookCategories"] = new JArray(),
				["conflictingItems"] = new JArray(),
				["usedInTasks"] = new JArray(),
				["receivedFromTasks"] = new JArray(),
				["bartersFor"] = new JArray(),
				["bartersUsing"] = new JArray(),
				["craftsFor"] = new JArray(),
				["craftsUsing"] = new JArray(),
			};

			if (sanitized["id"]?.Type != JTokenType.String) continue;
			sanitizedItems.Add(sanitized);
		}

		return new JObject {
			["data"] = new JObject {
				["data"] = sanitizedItems,
			},
		}.ToString(Formatting.None);
	}

	private static JToken? Translate(JToken? source, JObject locale) {
		string? key = source?.Value<string>();
		if (string.IsNullOrEmpty(key)) return source?.DeepClone();
		return locale.TryGetValue(key, out JToken? translated) ? translated.DeepClone() : source.DeepClone();
	}

	private static bool ValidateItemCache(string json) {
		FallbackResponse<Item>? response = JsonConvert.DeserializeObject<FallbackResponse<Item>>(json, JsonSettings);
		return response?.Data?.Data?.Length > 0 && response.Data.Data.All(item => !string.IsNullOrWhiteSpace(item?.Id));
	}

	private sealed class FallbackResponse<T> {
		[JsonProperty("data")]
		public FallbackResponseData<T>? Data { get; set; }
	}

	private sealed class FallbackResponseData<T> {
		[JsonProperty("data")]
		public T[]? Data { get; set; }
	}
}
