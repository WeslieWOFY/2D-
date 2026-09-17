using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OrangeCockroach : petbase
{
    protected override IEnumerator NormalAttackRoutine()
    {
        while(true)
        {
            PoolManager.Release(bulletPrefabs[0], spawnPoints[0].position);
            PoolManager.Release(bulletPrefabs[0], spawnPoints[1].position);
            AudioManager.Instance.PlaySFX(projectSFX);
            yield return new WaitForSeconds(attackInterval);
        }
    }
}
