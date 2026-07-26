using System;
using UnityEngine;

public class TunnelsLightBlink : MonoBehaviour
{
	public float blinkSpeed = 1.5f;

	private Renderer ren;

	private Color baseColor;

	private void Start()
	{
		ren = GetComponent<Renderer>();
		if (ren != null)
		{
			baseColor = ren.material.color;
		}
	}

	private void Update()
	{
		if (ren != null)
		{
			bool flag = (Mathf.Sin(Time.time * blinkSpeed * (float)Math.PI * 2f) + 1f) / 2f > 0.5f;
			ren.material.SetColor("_EmissionColor", flag ? (baseColor * 3.5f) : Color.clear);
		}
	}
}
