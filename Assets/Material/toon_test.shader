// Shader created with Shader Forge v1.40 
// Shader Forge (c) Freya Holmer - http://www.acegikmo.com/shaderforge/
// Note: Manually altering this data may prevent you from opening it in Shader Forge
/*SF_DATA;ver:1.40;sub:START;pass:START;ps:flbk:,iptp:0,cusa:False,bamd:0,cgin:,cpap:True,lico:1,lgpr:1,limd:0,spmd:1,trmd:0,grmd:0,uamb:True,mssp:True,bkdf:False,hqlp:False,rprd:False,enco:False,rmgx:True,imps:True,rpth:0,vtps:0,hqsc:True,nrmq:1,nrsp:0,vomd:0,spxs:False,tesm:0,olmd:1,culm:0,bsrc:0,bdst:1,dpts:2,wrdp:True,dith:0,atcv:False,rfrpo:True,rfrpn:Refraction,coma:15,ufog:False,aust:True,igpj:False,qofs:0,qpre:1,rntp:1,fgom:False,fgoc:False,fgod:False,fgor:False,fgmd:0,fgcr:0.5,fgcg:0.5,fgcb:0.5,fgca:1,fgde:0.01,fgrn:0,fgrf:300,stcl:False,atwp:False,stva:128,stmr:255,stmw:255,stcp:6,stps:0,stfa:0,stfz:0,ofsf:0,ofsu:0,f2p0:False,fnsp:False,fnfb:False,fsmp:False;n:type:ShaderForge.SFN_Final,id:3138,x:34259,y:32661,varname:node_3138,prsc:2|custl-318-OUT;n:type:ShaderForge.SFN_LightVector,id:268,x:32637,y:32689,varname:node_268,prsc:2;n:type:ShaderForge.SFN_NormalVector,id:3285,x:32637,y:32820,prsc:2,pt:True;n:type:ShaderForge.SFN_Dot,id:5612,x:32814,y:32725,varname:node_5612,prsc:2,dt:0|A-268-OUT,B-3285-OUT;n:type:ShaderForge.SFN_Step,id:3321,x:33171,y:32787,varname:node_3321,prsc:2|A-729-OUT,B-1678-OUT;n:type:ShaderForge.SFN_Slider,id:729,x:32800,y:32996,ptovrint:False,ptlb:toon_edge,ptin:_toon_edge,varname:node_729,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,min:-0.5,cur:0,max:0.5;n:type:ShaderForge.SFN_Color,id:8813,x:33171,y:32951,ptovrint:False,ptlb:color_light,ptin:_color_light,varname:node_8813,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,c1:0.9528302,c2:0.8735942,c3:0.7236116,c4:1;n:type:ShaderForge.SFN_Multiply,id:7629,x:33432,y:32842,varname:node_7629,prsc:2|A-3321-OUT,B-8813-RGB;n:type:ShaderForge.SFN_RemapRange,id:787,x:33419,y:33086,varname:node_787,prsc:2,frmn:0,frmx:1,tomn:1,tomx:0|IN-3321-OUT;n:type:ShaderForge.SFN_Multiply,id:5938,x:33645,y:33086,varname:node_5938,prsc:2|A-787-OUT,B-6305-RGB;n:type:ShaderForge.SFN_Color,id:6305,x:33388,y:33314,ptovrint:False,ptlb:color_dark,ptin:_color_dark,varname:node_6305,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,c1:0.2296191,c2:0.2802725,c3:0.3773585,c4:1;n:type:ShaderForge.SFN_Add,id:6274,x:33804,y:32881,varname:node_6274,prsc:2|A-7629-OUT,B-5938-OUT;n:type:ShaderForge.SFN_Tex2d,id:948,x:33804,y:32688,ptovrint:False,ptlb:Tex,ptin:_Tex,varname:node_948,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,ntxv:0,isnm:False;n:type:ShaderForge.SFN_Multiply,id:318,x:34042,y:32807,varname:node_318,prsc:2|A-948-RGB,B-6274-OUT,C-9622-RGB;n:type:ShaderForge.SFN_LightColor,id:9622,x:33804,y:33017,varname:node_9622,prsc:2;n:type:ShaderForge.SFN_LightAttenuation,id:2409,x:32931,y:32840,varname:node_2409,prsc:2;n:type:ShaderForge.SFN_Multiply,id:1678,x:33035,y:32677,varname:node_1678,prsc:2|A-5612-OUT,B-2409-OUT;proporder:729-8813-6305-948;pass:END;sub:END;*/

Shader "Shader Forge/toon_test" {
    Properties {
        _toon_edge ("toon_edge", Range(-0.5, 0.5)) = 0
        _color_light ("color_light", Color) = (0.9528302,0.8735942,0.7236116,1)
        _color_dark ("color_dark", Color) = (0.2296191,0.2802725,0.3773585,1)
        _Tex ("Tex", 2D) = "white" {}
    }
    SubShader {
        Tags {
            "RenderType"="Opaque"
        }
        Pass {
            Name "FORWARD"
            Tags {
                "LightMode"="ForwardBase"
            }
            
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            #include "AutoLight.cginc"
            #include "Lighting.cginc"
            #pragma multi_compile_fwdbase_fullshadows
            #pragma target 3.0
            uniform sampler2D _Tex; uniform float4 _Tex_ST;
            UNITY_INSTANCING_BUFFER_START( Props )
                UNITY_DEFINE_INSTANCED_PROP( float, _toon_edge)
                UNITY_DEFINE_INSTANCED_PROP( float4, _color_light)
                UNITY_DEFINE_INSTANCED_PROP( float4, _color_dark)
            UNITY_INSTANCING_BUFFER_END( Props )
            struct VertexInput {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 texcoord0 : TEXCOORD0;
            };
            struct VertexOutput {
                float4 pos : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                float2 uv0 : TEXCOORD0;
                float4 posWorld : TEXCOORD1;
                float3 normalDir : TEXCOORD2;
                LIGHTING_COORDS(3,4)
            };
            VertexOutput vert (VertexInput v) {
                VertexOutput o = (VertexOutput)0;
                UNITY_SETUP_INSTANCE_ID( v );
                UNITY_TRANSFER_INSTANCE_ID( v, o );
                o.uv0 = v.texcoord0;
                o.normalDir = UnityObjectToWorldNormal(v.normal);
                o.posWorld = mul(unity_ObjectToWorld, v.vertex);
                float3 lightColor = _LightColor0.rgb;
                o.pos = UnityObjectToClipPos( v.vertex );
                TRANSFER_VERTEX_TO_FRAGMENT(o)
                return o;
            }
            float4 frag(VertexOutput i) : COLOR {
                UNITY_SETUP_INSTANCE_ID( i );
                i.normalDir = normalize(i.normalDir);
                float3 normalDirection = i.normalDir;
                float3 lightDirection = normalize(_WorldSpaceLightPos0.xyz);
                float3 lightColor = _LightColor0.rgb;
////// Lighting:
                float attenuation = LIGHT_ATTENUATION(i);
                float4 _Tex_var = tex2D(_Tex,TRANSFORM_TEX(i.uv0, _Tex));
                float _toon_edge_var = UNITY_ACCESS_INSTANCED_PROP( Props, _toon_edge );
                float node_3321 = step(_toon_edge_var,(dot(lightDirection,normalDirection)*attenuation));
                float4 _color_light_var = UNITY_ACCESS_INSTANCED_PROP( Props, _color_light );
                float4 _color_dark_var = UNITY_ACCESS_INSTANCED_PROP( Props, _color_dark );
                float3 finalColor = (_Tex_var.rgb*((node_3321*_color_light_var.rgb)+((node_3321*-1.0+1.0)*_color_dark_var.rgb))*_LightColor0.rgb);
                return fixed4(finalColor,1);
            }
            ENDCG
        }
        Pass {
            Name "FORWARD_DELTA"
            Tags {
                "LightMode"="ForwardAdd"
            }
            Blend One One
            
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            #include "AutoLight.cginc"
            #include "Lighting.cginc"
            #pragma multi_compile_fwdadd_fullshadows
            #pragma target 3.0
            uniform sampler2D _Tex; uniform float4 _Tex_ST;
            UNITY_INSTANCING_BUFFER_START( Props )
                UNITY_DEFINE_INSTANCED_PROP( float, _toon_edge)
                UNITY_DEFINE_INSTANCED_PROP( float4, _color_light)
                UNITY_DEFINE_INSTANCED_PROP( float4, _color_dark)
            UNITY_INSTANCING_BUFFER_END( Props )
            struct VertexInput {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 texcoord0 : TEXCOORD0;
            };
            struct VertexOutput {
                float4 pos : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                float2 uv0 : TEXCOORD0;
                float4 posWorld : TEXCOORD1;
                float3 normalDir : TEXCOORD2;
                LIGHTING_COORDS(3,4)
            };
            VertexOutput vert (VertexInput v) {
                VertexOutput o = (VertexOutput)0;
                UNITY_SETUP_INSTANCE_ID( v );
                UNITY_TRANSFER_INSTANCE_ID( v, o );
                o.uv0 = v.texcoord0;
                o.normalDir = UnityObjectToWorldNormal(v.normal);
                o.posWorld = mul(unity_ObjectToWorld, v.vertex);
                float3 lightColor = _LightColor0.rgb;
                o.pos = UnityObjectToClipPos( v.vertex );
                TRANSFER_VERTEX_TO_FRAGMENT(o)
                return o;
            }
            float4 frag(VertexOutput i) : COLOR {
                UNITY_SETUP_INSTANCE_ID( i );
                i.normalDir = normalize(i.normalDir);
                float3 normalDirection = i.normalDir;
                float3 lightDirection = normalize(lerp(_WorldSpaceLightPos0.xyz, _WorldSpaceLightPos0.xyz - i.posWorld.xyz,_WorldSpaceLightPos0.w));
                float3 lightColor = _LightColor0.rgb;
////// Lighting:
                float attenuation = LIGHT_ATTENUATION(i);
                float4 _Tex_var = tex2D(_Tex,TRANSFORM_TEX(i.uv0, _Tex));
                float _toon_edge_var = UNITY_ACCESS_INSTANCED_PROP( Props, _toon_edge );
                float node_3321 = step(_toon_edge_var,(dot(lightDirection,normalDirection)*attenuation));
                float4 _color_light_var = UNITY_ACCESS_INSTANCED_PROP( Props, _color_light );
                float4 _color_dark_var = UNITY_ACCESS_INSTANCED_PROP( Props, _color_dark );
                float3 finalColor = (_Tex_var.rgb*((node_3321*_color_light_var.rgb)+((node_3321*-1.0+1.0)*_color_dark_var.rgb))*_LightColor0.rgb);
                return fixed4(finalColor * 1,0);
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
    CustomEditor "ShaderForgeMaterialInspector"
}
