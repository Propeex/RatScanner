using RatScanner.TarkovDev.GraphQL;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace RatScanner;

/// <summary>
/// Seeds empty offline caches for non-critical Tarkov.dev datasets.
/// This allows RatScanner to start when an optional API query fails during a cold start.
/// Existing caches are never overwritten.
/// </summary>
internal static class TarkovDevStartupFallback {
	private const string EmptyCacheResponse = "{\"data\":{\"data\":[]}}";

	[ModuleInitializer]
	internal static void Initialize() {
		try {
			foreach (LanguageCode language in Enum.GetValues<LanguageCode>()) {
				foreach (GameMode gameMode in Enum.GetValues<GameMode>()) {
					EnsureOptionalCache($"tasks_{language}_{gameMode}");
					EnsureOptionalCache($"hideout_{language}_{gameMode}");
					EnsureOptionalCache($"maps_{language}_{gameMode}");
				}
			}
		} catch (Exception exception) {
			// A module initializer must never prevent the application from loading.
			Debug.WriteLine($"Unable to initialize Tarkov.dev fallback caches: {exception}");
		}
	}

	private static void EnsureOptionalCache(string key) {
		if (RatConfig.ReadFromCache(key, out _)) return;
		RatConfig.WriteToCache(key, EmptyCacheResponse);
	}
}
