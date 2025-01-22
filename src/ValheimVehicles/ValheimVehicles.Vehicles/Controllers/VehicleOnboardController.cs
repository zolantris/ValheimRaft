using System.Collections.Generic;
using System.Linq;
using DynamicLocations.Controllers;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Serialization;
using ValheimVehicles.Config;
using ValheimVehicles.Helpers;
using ValheimVehicles.Patches;
using ValheimVehicles.Vehicles.Components;
using ValheimVehicles.Vehicles.Interfaces;
using Logger = Jotunn.Logger;

namespace ValheimVehicles.Vehicles.Controllers;

/// <summary>
/// A Controller placed directly on the VehicleOnboardCollider GameObject, meant to detect collisions only on that component
///
/// TODO in multiplayer make sure that not only the host, but all clients add all Characters that are players to the VehiclePieces controller. This way there is no jitters
/// </summary>
public class VehicleOnboardController : MonoBehaviour
{
  public VehicleMovementController? MovementController => vehicleShip != null
    ? vehicleShip.MovementController
    : null;

  public VehicleShip? vehicleShip;

  [UsedImplicitly]
  public static readonly Dictionary<ZDOID, WaterZoneCharacterData>
    CharacterOnboardDataItems =
      new();

  private static readonly Dictionary<ZDOID, Player> DelayedExitSubscriptions =
    [];

  public List<Player> m_localPlayers = [];


  public bool HasPlayersOnboard => m_localPlayers.Count > 0;
  private static bool _hasExitSubscriptionDelay = false;

  public BoxCollider OnboardCollider = null!;

  public VehiclePiecesController? PiecesController =>
    vehicleShip != null ? vehicleShip.PiecesController : null;

  private void Awake()
  {
    OnboardCollider = GetComponent<BoxCollider>();
    InvokeRepeating(nameof(ValidateCharactersAreOnShip), 1f, 30f);
  }

  public List<Player> GetPlayersOnShip()
  {
    var playerList = new List<Player>();
    var characterList = CharacterOnboardDataItems.Values
      .ToList();
    foreach (var characterOnboardDataItem in characterList)
    {
      if (characterOnboardDataItem == null) continue;
      if (characterOnboardDataItem.character == null) continue;
      if (!characterOnboardDataItem.character.IsPlayer()) continue;
      var piecesController = characterOnboardDataItem?.OnboardController
        ?.PiecesController;
      if (!piecesController && characterOnboardDataItem?.zdoId != null)
      {
        CharacterOnboardDataItems.Remove(characterOnboardDataItem.zdoId);
        continue;
      }

      if (piecesController == PiecesController)
      {
        var player = characterOnboardDataItem.character as Player;
        if (player == null) continue;
        playerList.Add(player);
      }
    }

    return playerList;
  }

  private bool IsValidCharacter(Character character)
  {
    return character != null && character.enabled;
  }

  private void ValidateCharactersAreOnShip()
  {
    var itemsToRemove = new List<Character>();
    var keysToRemove = new List<ZDOID>();

    foreach (var keyValuePair in CharacterOnboardDataItems)
    {
      // todo maybe add a check to see if character is connected.
      if (!IsValidCharacter(keyValuePair.Value.character))
      {
        keysToRemove.Add(keyValuePair.Key);
        continue;
      }

      if (keyValuePair.Value.character.transform.root
            .GetComponentInParent<VehiclePiecesController>() == null)
        itemsToRemove.Add(keyValuePair.Value.character);
    }

    foreach (var zdoid in keysToRemove) RemoveByZdoid(zdoid);

    foreach (var character in itemsToRemove) RemoveCharacter(character);
  }

  private static void RemoveByZdoid(ZDOID zdoid)
  {
    CharacterOnboardDataItems.Remove(zdoid);
  }

  public void TryAddPlayerIfMissing(Player player)
  {
    AddPlayerToLocalShip(player);
    AddCharacter(player);
  }

  private void RemoveCharacter(Character character)
  {
    var zdoid = character.GetZDOID();
    RemoveByZdoid(zdoid);

    var player = m_localPlayers
      .FirstOrDefault(x => x.GetZDOID() == zdoid);
    if (player != null)
      m_localPlayers.Remove(player);

    character.InNumShipVolumes--;
    WaterZoneUtils.UpdateDepthValues(character);
  }

  public void AddCharacter(Character character)
  {
    var zdoid = character.GetZDOID();
    var exists =
      CharacterOnboardDataItems.TryGetValue(zdoid,
        out var characterInstance);
    if (!exists)
    {
      var onboardDataItem = new WaterZoneCharacterData(character, this);
      CharacterOnboardDataItems.Add(zdoid, onboardDataItem);
      character.InNumShipVolumes++;
    }
    else if (characterInstance != null)
    {
      if (characterInstance.OnboardController != this ||
          (characterInstance.OnboardController != null &&
           characterInstance.OnboardController.transform.parent == null))
        characterInstance.OnboardController = this;
    }
  }

  public static bool IsCharacterOnboard(Character character)
  {
    return CharacterOnboardDataItems.ContainsKey(character.GetZDOID());
  }

  public static void UpdateUnderwaterState(Character character, bool? val)
  {
    var characterData = GetOnboardCharacterData(character);
    characterData?.UpdateUnderwaterStatus(val);
  }

  public static WaterZoneCharacterData? GetOnboardCharacterData(
    Character character)
  {
    return GetOnboardCharacterData(character.GetZDOID());
  }

  public static WaterZoneCharacterData? GetOnboardCharacterData(
    ZDOID zdoid)
  {
    if (CharacterOnboardDataItems.TryGetValue(zdoid,
          out var data))
    {
      if (data.OnboardController == null)
      {
        CharacterOnboardDataItems.Remove(zdoid);
        return null;
      }

      return data;
    }

    return null;
  }

  public static bool GetCharacterVehicleMovementController(ZDOID zdoid,
    out VehicleOnboardController? controller)
  {
    controller = null;
    if (CharacterOnboardDataItems.TryGetValue(zdoid, out var data))
    {
      if (data.OnboardController == null)
        data.OnboardController =
          VehiclePiecesController
            .GetPieceControllerFromPlayer(data.character.gameObject)?
            .VehicleInstance?.OnboardController;

      if (data.OnboardController == null)
      {
        CharacterOnboardDataItems.Remove(zdoid);
        return false;
      }

      controller = data.OnboardController;
      return true;
    }

    controller = null;
    return false;
  }

  private Coroutine? _removePlayersCoroutineInstance;

  /// <summary>
  /// Starts the updater only for server or client hybrid but not client only
  /// </summary>
  private void StartRemovePlayerCoroutine()
  {
    if (ZNet.instance == null) return;
    if (ZNet.instance.IsDedicated())
    {
      _removePlayersCoroutineInstance = StartCoroutine(RemovePlayersRoutine());
      return;
    }

    if (!ZNet.instance.IsServer() && !ZNet.instance.IsDedicated())
      _removePlayersCoroutineInstance = StartCoroutine(RemovePlayersRoutine());
  }

  private void OnEnable()
  {
    StartRemovePlayerCoroutine();
  }

  private void OnDisable()
  {
    // protect character so it removes this list on unmount of onboard controller
    foreach (var character in CharacterOnboardDataItems.Values.ToList())
      if (character.OnboardController == this)
        CharacterOnboardDataItems.Remove(character.zdoId);

    if (_removePlayersCoroutineInstance != null)
      StopCoroutine(_removePlayersCoroutineInstance);
  }

  public void OnTriggerEnter(Collider collider)
  {
    if (MovementController == null) return;
    OnPlayerEnterVehicleBounds(collider);
    HandleCharacterHitVehicleBounds(collider, false);
  }

  public void OnTriggerExit(Collider collider)
  {
    if (MovementController == null) return;
    HandlePlayerExitVehicleBounds(collider);
    HandleCharacterHitVehicleBounds(collider, true);
  }

  public void RestoreCollisionDetection(Collider collider)
  {
    if (PiecesController != null &&
        PiecesController.convexHullMeshColliders.Count > 0)
      foreach (var piecesControllerConvexHullMesh in
               PiecesController.convexHullMeshColliders)
        Physics.IgnoreCollision(piecesControllerConvexHullMesh, collider,
          false);
  }

  public void HandleCharacterHitVehicleBounds(Collider collider, bool isExiting)
  {
    var character = collider.GetComponent<Character>();
    if (character == null) return;

    RestoreCollisionDetection(collider);

    if (isExiting)
    {
      RemoveCharacter(character);
      return;
    }

    // do not increment or add character if already exists in object. This could be a race condition
    AddCharacter(character);

    WaterZoneUtils.UpdateDepthValues(character, LiquidType.Water);
  }

  /// <summary>
  /// Gets the PlayerComponent and adds/removes it based on exiting state
  /// </summary>
  /// <param name="collider"></param>
  /// <returns></returns>
  private Player? GetPlayerComponent(Collider collider)
  {
    if (MovementController.VehicleInstance?.Instance == null) return null;
    var playerComponent = collider.GetComponent<Player>();
    if (!playerComponent) return null;

#if DEBUG
    Logger.LogDebug("Player collider hit OnboardTriggerCollider");
#endif

    return playerComponent;
  }

  /// <summary>
  /// Restores the blocking behavior if this mod is controlling / unblocking camera
  /// </summary>
  /// <param name="player"></param>
  public static void RestorePlayerBlockingCamera(Player player)
  {
    if (!PhysicsConfig.removeCameraCollisionWithObjectsOnBoat.Value) return;
    if (Player.m_localPlayer == player &&
        GameCamera.instance.m_blockCameraMask == 0)
      GameCamera.instance.m_blockCameraMask =
        GameCamera_WaterPatches.BlockingWaterMask;
  }

  public static void AddOrRemovePlayerBlockingCamera(Player player)
  {
    if (WaterZoneUtils.IsOnboard(player))
      RemovePlayerBlockingCameraWhileOnboard(player);
    else
      RestorePlayerBlockingCamera(player);
  }

  /// <summary>
  /// Prevents jitters. Likely most people will want this feature enabled especially for complicated boats.
  /// </summary>
  /// Does not remove changes if the feature is disabled. Players will need to reload. This prevents breaking other mods that might mess with camera.
  /// <param name="player"></param>
  public static void RemovePlayerBlockingCameraWhileOnboard(Player player)
  {
    if (!PhysicsConfig.removeCameraCollisionWithObjectsOnBoat.Value) return;
    if (Player.m_localPlayer == player &&
        GameCamera.instance.m_blockCameraMask != 0)
      GameCamera.instance.m_blockCameraMask = 0;
  }

  private void RemovePlayerOnShip(Player player)
  {
    var isPlayerInList = m_localPlayers.Contains(player);
    player.m_doodadController = null;

    if (isPlayerInList)
    {
      m_localPlayers.Remove(player);
      if (Player.m_localPlayer == player)
        ValheimBaseGameShip.s_currentShips.Remove(MovementController);
    }
    else
    {
      Logger.LogWarning(
        $"Player {player.GetPlayerName()} detected leaving ship, but not within the ship's player list");
    }

    RestorePlayerBlockingCamera(player);

    player.transform.SetParent(null);
  }

  public void AddPlayerToLocalShip(Player player)
  {
    if (PiecesController == null) return;

    var piecesTransform = PiecesController.transform;

    if (!piecesTransform)
    {
      Logger.LogDebug("Unable to get piecesControllerTransform.");
      return;
    }

    var isPlayerInList = m_localPlayers.Contains(player);
    RemovePlayerBlockingCameraWhileOnboard(player);
    player.transform.SetParent(piecesTransform);

    if (!isPlayerInList)
      m_localPlayers.Add(player);
    else
      Logger.LogWarning(
        "Player detected entering ship, but they are already added within the list of ship players");
  }

  /// <summary>
  /// Protects against the vehicle smashing the player out of the world on spawn.
  /// </summary>
  /// <param name="character"></param>
  public void OnEnterVolatileVehicle(Character character)
  {
    if (PiecesController == null) return;
    if (!PiecesController.IsActivationComplete)
      character.m_body.isKinematic = true;
  }

  public static void OnVehicleReady()
  {
    foreach (var characterOnboardDataItem in CharacterOnboardDataItems)
      if (characterOnboardDataItem.Value.character.m_body.isKinematic)
        characterOnboardDataItem.Value.character.m_body.isKinematic = false;
  }

  public static void RPC_PlayerOnboardSync()
  {
  }

  public void OnPlayerEnterVehicleBounds(Collider collider)
  {
    var playerInList = GetPlayerComponent(collider);
    if (playerInList == null) return;

    // All clients should do this
    AddPlayerToLocalShip(playerInList);
    if (Player.m_localPlayer == playerInList)
      ValheimBaseGameShip.s_currentShips.Add(MovementController);

    Logger.LogDebug(
      $"Player: {playerInList.GetPlayerName()} on-board, total onboard {m_localPlayers.Count}");

    var vehicleZdo = MovementController.VehicleInstance?.NetView != null
      ? MovementController.VehicleInstance.NetView.GetZDO()
      : null;

    if (playerInList == Player.m_localPlayer && vehicleZdo != null)
      if (PlayerSpawnController.Instance != null)
        PlayerSpawnController.Instance.SyncLogoutPoint(vehicleZdo);
  }

  public void RemoveLogoutPoint(
    KeyValuePair<ZDOID, Player> delayedExitSubscription)
  {
    if (MovementController == null ||
        MovementController.VehicleInstance?.NetView == null) return;
    var vehicleZdo = MovementController
      .VehicleInstance.NetView.GetZDO();
    if (delayedExitSubscription.Value == Player.m_localPlayer &&
        vehicleZdo != null && PlayerSpawnController.Instance != null)
      PlayerSpawnController.Instance.SyncLogoutPoint(vehicleZdo, true);
  }

  public void DebounceExitVehicleBounds()
  {
    _hasExitSubscriptionDelay = true;
    var localList = DelayedExitSubscriptions.ToList();

    // allows new items to be added while this is running
    DelayedExitSubscriptions.Clear();

    foreach (var delayedExitSubscription in localList)
    {
      RemovePlayerOnShip(delayedExitSubscription.Value);
      var remainingPlayers = m_localPlayers.Count;
      Logger.LogDebug(
        $"Player: {delayedExitSubscription.Value.GetPlayerName()} over-board, players remaining {remainingPlayers}");
      RemoveLogoutPoint(delayedExitSubscription);
    }

    _hasExitSubscriptionDelay = false;
  }


  public void HandlePlayerExitVehicleBounds(Collider collider)
  {
    var playerInList = GetPlayerComponent(collider);
    if (playerInList == null) return;

    var playerZdoid = playerInList.GetZDOID();
    if (!DelayedExitSubscriptions.ContainsKey(playerZdoid))
      DelayedExitSubscriptions.Add(playerZdoid, playerInList);

    if (!_hasExitSubscriptionDelay)
    {
      _hasExitSubscriptionDelay = true;
      Invoke(nameof(DebounceExitVehicleBounds), 0.5f);
    }
  }

  /// <summary>
  /// Coroutine to update players if they logout or desync, this will remove them every 30 seconds
  /// </summary>
  /// <returns></returns>
  private IEnumerator<WaitForSeconds?> RemovePlayersRoutine()
  {
    while (isActiveAndEnabled)
    {
      yield return new WaitForSeconds(15);

      if (PiecesController == null) continue;

      var playersOnboard = PiecesController.GetComponentsInChildren<Player>();
      List<Player> validPlayers = [];

      if (playersOnboard == null) continue;

      foreach (var player in playersOnboard)
      {
        if (player == null || !player.isActiveAndEnabled) continue;
        validPlayers.Add(player);
      }

      if (MovementController != null)
      {
        m_localPlayers = validPlayers;
        if (validPlayers.Count == 0) MovementController.SendDelayedAnchor();
      }

      yield return new WaitForSeconds(15);
    }
  }
}