
using System;
using UnityEngine;

namespace OopsAllGup
{
    class GupDetails : MonoBehaviour
    {
        public int livesLeft;

        public void CopyFrom(GupDetails gupDetails)
        {
            livesLeft = gupDetails.livesLeft;
        }
    }
}
