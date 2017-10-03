using System;
using CoCEd.Model;

namespace CoCEd.ViewModel
{
    public class SkinLayerVM : ObjectVM
    {
        public SkinLayerVM(SkinVM skin, AmfObject obj) : base(obj)
        {
	        this._skin = skin;
        }

	    private SkinVM _skin;

        public int Type
        {
	        get => GetInt("type", 0);
	        set => SetValue("type", value);
        }

	    public string Color
	    {
		    get => GetString("color");
		    set => SetValue("color", value);
	    }
	    
	    public string SecondaryColor
	    {
		    get => GetString("color2");
		    set => SetValue("color2", value);
	    }

	    public int Pattern
	    {
		    get => GetInt("pattern", 0);
		    set
		    {
			    SetValue("pattern", value);
			    _skin.LayerPropertyChanged(this, "Pattern");
		    }
	    }

	    public string Description
	    {
		    get => GetString("desc");
		    set => SetValue("desc", value);
	    }

	    public string Adjective
	    {
		    get => GetString("adj");
		    set => SetValue("adj", value);
	    }
    }
}