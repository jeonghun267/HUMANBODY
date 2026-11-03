// 시야에 달라붙는 방사형 무늬가 없는 정적인 환경 색상.
float4 _BaseColor, _LightColor, _DeepColor, _AccentColor;
float _Mode, _FoldCount, _FoldStrength, _RingStrength, _Detail, _Tunnel, _Exposure;
half4 JourneyEnvironmentColor(float3 direction)
{
    float up = normalize(direction).y * .5 + .5;
    float3 color;
    if (_Mode < .5) color = lerp(_BaseColor.rgb, _LightColor.rgb, smoothstep(.2,1,up)*.65);
    else if (_Mode > 1.5) color = lerp(_DeepColor.rgb, _BaseColor.rgb, up*.65);
    else color = lerp(_BaseColor.rgb*.76, _BaseColor.rgb*.94, smoothstep(0,1,up));
    return half4(max(color*_Exposure,0),1);
}
