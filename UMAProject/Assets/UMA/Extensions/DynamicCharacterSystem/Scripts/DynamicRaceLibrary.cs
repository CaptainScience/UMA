using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections;
using System.Collections.Generic;
using UMA;
using UMAAssetBundleManager;

public class DynamicRaceLibrary : RaceLibrary
{

	//extra fields for Dynamic Version
	public bool dynamicallyAddFromResources;
	[Tooltip("Limit the Resources search to the following folders (no starting slash and seperate multiple entries with a comma)")]
	public string resourcesFolderPath = "";
	public bool dynamicallyAddFromAssetBundles;
	[Tooltip("Limit the AssetBundles search to the following bundles (no starting slash and seperate multiple entries with a comma)")]
	public string assetBundleNamesToSearch = "";
	//This is a ditionary of asset bundles that were loaded into the library at runtime. 
	//CharacterAvatar can query this this to find out what asset bundles were required to create itself 
	//or other scripts can use it to find out which asset bundles are being used by the Libraries at any given point.
	public Dictionary<string, List<string>> assetBundlesUsedDict = new Dictionary<string, List<string>>();
#if UNITY_EDITOR
	//if we have already added everything in the editor dont do it again
	bool allAssetsAddedInEditor = false;
	[HideInInspector]
	List<RaceData> editorAddedAssets = new List<RaceData>();
#endif
	[System.NonSerialized]
	bool allStartingAssetsAdded = false;

	[System.NonSerialized]
	[HideInInspector]
	public bool downloadAssetsEnabled = true;

#if UNITY_EDITOR
	void Reset()
	{
		allStartingAssetsAdded = false;//make this false to that loading new scenes makes the library update
		allAssetsAddedInEditor = false;
	}
#endif

	public void Awake()
	{
#if UNITY_EDITOR
		allStartingAssetsAdded = false;//make this false to that loading new scenes makes the library update
		allAssetsAddedInEditor = false;
#endif
	}

	public void Start()
	{
		if (Application.isPlaying)
		{
			assetBundlesUsedDict.Clear();
		}
#if UNITY_EDITOR
		if (Application.isPlaying)
		{
			ClearEditorAddedAssets();
		}
		allStartingAssetsAdded = false;//make this false to that loading new scenes makes the library update
		allAssetsAddedInEditor = false;
#endif
	}

	/// <summary>
	/// Clears any editor added assets when the Scene is closed
	/// </summary>
	void OnDestroy()
	{
#if UNITY_EDITOR
		ClearEditorAddedAssets();
#endif
	}

	public void ClearEditorAddedAssets()
	{
#if UNITY_EDITOR
		if (editorAddedAssets.Count > 0)
		{
			editorAddedAssets.Clear();
			allAssetsAddedInEditor = false;
		}
#endif
	}

#if UNITY_EDITOR
	RaceData GetEditorAddedAsset(int? raceHash = null, string raceName = "")
	{
		RaceData foundRaceData = null;
		if (editorAddedAssets.Count > 0)
		{
			foreach (RaceData edRace in editorAddedAssets)
			{
				if (edRace != null)
				{
					if (raceHash != null)
					{
						if (UMAUtils.StringToHash(edRace.raceName) == raceHash)
							foundRaceData = edRace;
					}
					else if (raceName != null)
					{
						if (raceName != "")
							if (edRace.raceName == raceName)
								foundRaceData = edRace;
					}
				}
			}
		}
		return foundRaceData;
	}
#endif
	//Loading speed issue does not seem to be related to this since commenting the AddAssets calls out makes no difference!?!
	public void UpdateDynamicRaceLibrary(bool downloadAssets, int? raceHash = null)
	{
		Debug.LogWarning("[DynamicRaceLibrary] UpdateDynamicRaceLibrary called");
		if (allStartingAssetsAdded)
		{
			Debug.Log("[DynamicRaceLibrary] Did not update because allStartingAssetsAdded was true");
			return;
		}
		//Making the race library scan everything should only happen once- at all other times a specific race should have been requested (and been added by dynamic asset loader) so it should already be here if it needs to be.
		if (raceHash == null && Application.isPlaying && allStartingAssetsAdded == false && UMAResourcesIndex.Instance.initialized && UMAResourcesIndex.Instance.enableDynamicIndexing == false)
		{
			Debug.LogWarning("[DynamicRaceLibrary] UpdateDynamicRaceLibrary searched for everything- This should only happen ONCE");
			allStartingAssetsAdded = true;
		}
#if UNITY_EDITOR
		if (allAssetsAddedInEditor)
		{
			Debug.Log("[DynamicRaceLibrary] Did not update because allAssetsAddedInEditor was true");
			return;
		}
#endif
		DynamicAssetLoader.Instance.AddAssets<RaceData>(ref assetBundlesUsedDict, dynamicallyAddFromResources, dynamicallyAddFromAssetBundles, downloadAssets, assetBundleNamesToSearch, resourcesFolderPath, raceHash, "", AddRaces);

#if UNITY_EDITOR
		if (raceHash == null && !Application.isPlaying)
		{
			allAssetsAddedInEditor = true;
		}
#endif
	}

	public void UpdateDynamicRaceLibrary(string raceName)
	{
		DynamicAssetLoader.Instance.AddAssets<RaceData>(ref assetBundlesUsedDict, dynamicallyAddFromResources, dynamicallyAddFromAssetBundles, downloadAssetsEnabled, assetBundleNamesToSearch, resourcesFolderPath, null, raceName, AddRaces);
	}

	private void AddRaces(RaceData[] races)
	{
		foreach (RaceData race in races)
		{
#if UNITY_EDITOR
			if (!Application.isPlaying)
			{
				if (!editorAddedAssets.Contains(race))
				{
					editorAddedAssets.Add(race);
					if (UMAContext.Instance.dynamicCharacterSystem != null)
					{
						(UMAContext.Instance.dynamicCharacterSystem as UMACharacterSystem.DynamicCharacterSystem).Refresh(false);
					}
				}
			}
			else
#endif
				AddRace(race);

		}
		//This doesn't actually seem to do anything apart from slow things down
		//StartCoroutine(CleanRacesFromResourcesAndBundles());
	}

	/*IEnumerator CleanRacesFromResourcesAndBundles()
    {
        yield return null;
        Resources.UnloadUnusedAssets();
        yield break;
    }*/

#pragma warning disable 618
	//We need to override AddRace Too because if the element is not in the list anymore it causes an error...
	override public void AddRace(RaceData race)
	{
		if (race == null)
			return;
		race.UpdateDictionary();
		int currentNumRaces = raceElementList.Length;
		try
		{
			base.AddRace(race);
		}
		catch
		{
			//if there is an error it will be because RaceElementList contained an empty refrence
			List<RaceData> newRaceElementList = new List<RaceData>();
			for (int i = 0; i < raceElementList.Length; i++)
			{
				if (raceElementList[i] != null)
				{
					raceElementList[i].UpdateDictionary();
					newRaceElementList.Add(raceElementList[i]);
				}
			}
			raceElementList = newRaceElementList.ToArray();
			base.AddRace(race);
		}
		if (currentNumRaces != raceElementList.Length)
		{
			if (UMAContext.Instance.dynamicCharacterSystem != null)
			{
				(UMAContext.Instance.dynamicCharacterSystem as UMACharacterSystem.DynamicCharacterSystem).Refresh(false);
			}
		}
	}
#pragma warning restore 618
	//TODO if this works it should maybe be the other way round for backwards compatability- i.e. so unless you do something different this does what it always did do...
	public override RaceData GetRace(string raceName)
	{
		return GetRace(raceName, true);
	}
	public RaceData GetRace(string raceName, bool allowUpdate = true)
	{
		if ((raceName == null) || (raceName.Length == 0))
			return null;

		RaceData res;
		res = base.GetRace(raceName);
#if UNITY_EDITOR
		if (!Application.isPlaying && res == null)
		{
			res = GetEditorAddedAsset(null, raceName);
		}
#endif
		if (res == null && allowUpdate)
		{
			//we try to load the race dynamically
			UpdateDynamicRaceLibrary(raceName);
			res = base.GetRace(raceName);
			if (res == null)
			{
#if UNITY_EDITOR
				if (!Application.isPlaying)
				{
					res = GetEditorAddedAsset(null, raceName);
					if (res != null)
					{
						return res;
					}
				}
#endif
				return null;
			}
		}
		return res;
	}
	public override RaceData GetRace(int nameHash)
	{
		return GetRace(nameHash, true);
	}
	public RaceData GetRace(int nameHash, bool allowUpdate = true)
	{
		if (nameHash == 0)
			return null;

		RaceData res;
		res = base.GetRace(nameHash);
#if UNITY_EDITOR
		if (!Application.isPlaying && res == null)
		{
			res = GetEditorAddedAsset(nameHash);
		}
#endif
		if (res == null && allowUpdate)
		{
			UpdateDynamicRaceLibrary(true, nameHash);
			res = base.GetRace(nameHash);
			if (res == null)
			{
#if UNITY_EDITOR
				if (!Application.isPlaying)
				{
					res = GetEditorAddedAsset(nameHash);
					if (res != null)
					{
						return res;
					}
				}
#endif
				return null;
			}
		}
		return res;
	}
	/// <summary>
	/// Returns the current list of races without adding from assetBundles or Resources
	/// </summary>
	/// <returns></returns>
	public RaceData[] GetAllRacesBase()
	{
		return base.GetAllRaces();
	}
	/// <summary>
	/// Gets all the races that are available including in Resources (but does not cause downloads for races that are in assetbundles)
	/// </summary>
	/// <returns></returns>
	public override RaceData[] GetAllRaces()
	{
		UpdateDynamicRaceLibrary(false);
#if UNITY_EDITOR
		if (!Application.isPlaying)
		{
			//we need a combined array of the editor added assets and the baseGetAllRaces Array
			List<RaceData> combinedRaceDatas = new List<RaceData>(base.GetAllRaces());
			if (editorAddedAssets.Count > 0)
			{
				combinedRaceDatas.AddRange(editorAddedAssets);
			}
			return combinedRaceDatas.ToArray();
		}
		else
#endif
			return base.GetAllRaces();
	}

	/// <summary>
	/// Gets the originating asset bundle.
	/// </summary>
	/// <returns>The originating asset bundle.</returns>
	/// <param name="raceName">Race name.</param>
	public string GetOriginatingAssetBundle(string raceName)
	{
		string originatingAssetBundle = "";
		if (assetBundlesUsedDict.Count > 0)
		{
			foreach (KeyValuePair<string, List<string>> kp in assetBundlesUsedDict)
			{
				if (kp.Value.Contains(raceName))
				{
					originatingAssetBundle = kp.Key;
					break;
				}
			}
		}
		if (originatingAssetBundle == "")
		{
			Debug.Log(raceName + " was not found in any loaded AssetBundle");
		}
		else
		{
			Debug.Log("originatingAssetBundle for " + raceName + " was " + originatingAssetBundle);
		}
		return originatingAssetBundle;
	}
}
