using System;
using UnityEngine;

// Instantiate a Feedback prefab if one hasn't been instantiated already.
// The Feedback GameObject is always the same in all our scenes, so we'll preserve it when scenes unload.

public class FeedbackBootstrapper : MonoBehaviour
{
    public GameObject feedbackPrefab;

    static GameObject m_FeedbackGameObject;

    void Awake()
    {
        if (m_FeedbackGameObject is null)
        {
            m_FeedbackGameObject = Instantiate(feedbackPrefab);

            DontDestroyOnLoad(m_FeedbackGameObject);
        }
    }
}
