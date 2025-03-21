using System.Collections.Generic;
using Arch.Core;
using FallingSand.Entity.Sprites;
using Microsoft.Xna.Framework;

namespace FallingSand.Entity.Component;

enum WeaponFireType
{
    Single,
    Burst,
    Auto,
}

enum WeaponType
{
    Pistol,
    SMG,
    Shotgun,
    RocketLauncher,
    GrenadeLauncher,
}

struct WeaponComponent(WeaponType weaponType)
{
    public WeaponType WeaponType = weaponType;
    public WeaponConfig Config = WeaponConfigManager.GetWeaponConfig(weaponType);
    public float LastFireTime = 0f;
}

class WeaponConfig
{
    public IEnumerable<BulletBehaviour> BulletBehaviours { get; set; }
    public WeaponFireType FireType { get; set; }
    public string SpriteName { get; set; }
    public string BulletSpriteName { get; set; }
    public float FireRate { get; set; }
    public float Damage { get; set; }
    public float BulletSpeed { get; set; }
    public int MaxAmmo { get; set; }
    public int BulletsPerShot { get; set; } = 1;
    public float Spread { get; set; } = 0f;
}

static class WeaponConfigManager
{
    private static readonly Dictionary<WeaponType, WeaponConfig> _weaponConfigs = [];

    static WeaponConfigManager()
    {
        RegisterWeaponTypes();
    }

    private static void RegisterWeaponTypes()
    {
        // Register different weapon types
        _weaponConfigs.Add(
            WeaponType.Pistol,
            new WeaponConfig
            {
                SpriteName = "Pistol",
                BulletSpriteName = "Bullet",
                BulletBehaviours = [],
                FireType = WeaponFireType.Single,
                FireRate = 1f,
                Damage = 10f,
                BulletSpeed = 20f,
                MaxAmmo = 12,
            }
        );

        _weaponConfigs.Add(
            WeaponType.SMG,
            new WeaponConfig
            {
                BulletBehaviours = [],
                FireType = WeaponFireType.Auto,
                FireRate = 0.3f,
                Damage = 10f,
                BulletSpeed = 20f,
                MaxAmmo = 12,
            }
        );

        _weaponConfigs.Add(
            WeaponType.Shotgun,
            new WeaponConfig
            {
                BulletBehaviours = [],
                FireType = WeaponFireType.Single,
                FireRate = 1.0f,
                Damage = 8f,
                BulletSpeed = 18f,
                MaxAmmo = 8,
                BulletsPerShot = 8,
                Spread = 0.2f,
            }
        );

        _weaponConfigs.Add(
            WeaponType.RocketLauncher,
            new WeaponConfig
            {
                BulletBehaviours = [BulletBehaviour.Explode],
                FireType = WeaponFireType.Single,
                FireRate = 2.0f,
                Damage = 25f,
                BulletSpeed = 15f,
                MaxAmmo = 4,
            }
        );

        _weaponConfigs.Add(
            WeaponType.GrenadeLauncher,
            new WeaponConfig
            {
                BulletBehaviours = [BulletBehaviour.Bounce, BulletBehaviour.Explode],
                FireType = WeaponFireType.Single,
                FireRate = 0.1f,
                Damage = 5f,
                BulletSpeed = 50f,
                MaxAmmo = 30,
            }
        );
    }

    public static WeaponConfig GetWeaponConfig(WeaponType weaponId)
    {
        if (_weaponConfigs.TryGetValue(weaponId, out var config))
            return config;

        // Return default if type not found
        return _weaponConfigs[WeaponType.Pistol];
    }
}
