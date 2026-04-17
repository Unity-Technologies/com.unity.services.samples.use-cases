using System.Collections.Generic;
using UnityEngine;
namespace GemHunterUGS.Scripts
{
  [CreateAssetMenu(fileName = "RandomProfilePictures", menuName = "2D Match/UGSIntegration/RandomProfilePictures")]
  public class RandomProfilePicturesSO : ScriptableObject
  {
    [field: SerializeField]
    public List<Sprite> ProfilePictures { get; private set; }
  }
}
