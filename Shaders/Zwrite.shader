Shader "Zwrite" {
    Properties {
	    _AlphaTex ("Alpha mask (R)", 2D) = "white" {}
	    _Cutoff ("Alpha cutoff", Range(0,1)) = 0.99
    }

    SubShader {
	    Tags {"Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent"}
	
	    Pass {
		    Lighting Off
            Zwrite On
            ColorMask 0
		    Alphatest Greater [_Cutoff]
		    SetTexture [_AlphaTex] { combine texture }
	    }
    }
}