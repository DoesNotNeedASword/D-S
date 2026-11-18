"""Merchant, apprentice, raiders and ember sentinel. Coordinates are metres, Y up."""
from math import sin, cos, pi
from modeling import Model


def actor(name, kind):
    m = Model()
    gob = kind in ("raider","shaman")
    giant = kind == "sentinel"
    human = not gob and not giant
    mage = kind == "shaman"
    trader = kind == "merchant"
    skin = "goblin" if gob else "basalt" if giant else "skin"
    coat = "plum" if mage else "wine" if trader else "obsidian" if giant else "leather" if gob else "olive"

    def ring(color, rows, sides=16, pleat=0):
        m.loft(color, [(0,y,z,xr,zr) for y,z,xr,zr in rows], sides, pleat)

    # Long folds broaden above the belt and at the hem, with a second visible lining.
    ring("brass" if mage else "grain" if gob else "linen", [(-.34,0,.37,.24),(-.29,0,.39,.25),(-.17,0,.32,.21)],20,.035)
    ring(coat,[(-.30,0,.39,.255),(-.235,-.005,.39,.25),(-.12,0,.32,.225),
               (-.055,0,.30,.225),(.10,-.008,.355,.235),(.22,-.014,.395,.23),(.31,-.008,.295,.185),(.34,0,.16,.14)],20,.035)
    ring("oak",[(-.137,0,.324,.235),(-.114,0,.339,.244),(-.046,0,.321,.235),(-.031,0,.309,.224)])
    def crest(color,x,y,z,w,h):
        m.plate(color,[(x-w,y+h*.80,z),(x,y+h,z+.012),(x+w,y+h*.78,z),
                      (x+w*.78,y-h*.55,z),(x,y-h,z+.014),(x-w*.78,y-h*.56,z)],.022,.009)
    crest("brass",0,-.084,.245,.072,.056)
    crest("oak",0,-.082,.265,.044,.029)
    for s in (-1,1):
        m.oval("grain",(s*.305,-.15,.115),(.087,.10,.067),10)
        m.band("brass",s*.31,-.112,.132,.073,.063,.022,10)
    if gob or trader:
        m.ribbon("grain",[(-.22,-.074,.197),(-.09,.029,.239),(.095,.146,.235),(.245,.265,.155)],
                 [.054,.06,.061,.054],.018)
        crest("iron",.065,.129,.267,.052,.043)
    if mage or giant:
        crest("brass" if mage else "crust",0,-.21,.252,.154,.19)
        crest("plumlight" if mage else "lava",0,-.20,.27,.106,.147)
    ring("plumlight" if mage else "wine" if gob else "crust" if giant else "linen",
         [(.21,.012,.32,.204),(.24,.016,.34,.218),(.285,.01,.31,.195),(.346,0,.199,.162)],16,.045)

    ring(skin,[(.286,0,.129,.118),(.36,0,.134,.117),(.425,.01,.123,.120)])
    if mage:
        ring("brass",[(-.549,0,.388,.25),(-.52,0,.396,.255),(-.43,0,.376,.241)],20,.045)
        ring("plum",[(-.51,0,.399,.256),(-.44,0,.39,.252),(-.16,0,.32,.229)],20,.045)
        for side in (-1,1):
            m.ribbon("brass",[(side*.193,-.51,.212),(side*.139,-.32,.224),(side*.10,-.146,.217)],
                     [.022,.018,.022],.01)
    if kind=="raider" or trader:
        for k in range(10):
            a=2*pi*k/10
            x,z=.312*cos(a),.199*sin(a)
            dy=.055*(k%3)
            m.loft("grain" if k%2 else "leather",[(x*1.08,-.375-dy,z*1.1,.028,.025),
                   (x*1.07,-.301,z*1.12,.087,.063),(x,-.207,z*1.05,.08,.053)],7)
    if kind=="apprentice":
        ring("grain",[(-.515,-.01,.332,.225),(-.47,-.01,.35,.239),(-.297,0,.353,.224)],20,.07)
        m.plate("olive",[(-.155,-.104,.22),(.156,-.104,.22),(.185,-.36,.234),(.033,-.456,.228),
                (-.195,-.378,.225)],.023,.015)
    # Facial planes taper forward from brow to nose and rounded jaw.
    head = .575
    ring(skin,[(head-.202,.038,.103,.12),(head-.175,.036,.186,.169),(head-.109,.025,.244,.205),
               (head-.035,.012,.284,.23),(head+.067,0,.293,.236),(head+.145,-.012,.278,.212),
               (head+.22,-.019,.218,.171),(head+.26,-.025,.106,.09)],20)
    if not giant:
        for s in (-1,1):
            eye_y = head+.045
            m.oval("cheek" if gob else "blush",(s*.173,head-.043,.186),(.098,.061,.052),12)
            m.oval("ink",(s*.123,eye_y,.218),(.092,.048,.028),14)
            m.oval("eye" if gob else "ivory",(s*.123,eye_y,.241),(.077,.035,.021),14)
            m.oval("ink",(s*.115,eye_y-.004,.261),(.025,.030,.01),12)
            m.oval("ivory",(s*.112-.008,eye_y+.013,.272),(.009,.01,.004),8)
            if human:
                m.ribbon("beard" if trader else "hairlight",[(s*.039,eye_y+.062,.247),(s*.113,eye_y+.082,.245),
                           (s*.19,eye_y+.064,.221)], [.016,.019,.008],.009)
            else: m.plate("cheek",[(s*.027,eye_y+.040,.257),(s*.069,eye_y+.088,.246),
                    (s*.202,eye_y+.105,.206),(s*.223,eye_y+.075,.201),(s*.113,eye_y+.054,.239)],.034,.010)
        m.loft("goblin" if gob else "skin",[(0,head-.085,.233,.036,.033),(0,head-.063,.263,.071,.065),
               (0,head-.02,.257,.059,.060),(0,head+.084,.218,.036,.027)],12)
        m.plate("lip",[(-.093,head-.12,.211),(-.035,head-.107,.235),(.028,head-.109,.237),(.096,head-.117,.213),
                (.059,head-.135,.221),(-.041,head-.135,.227)],.008,.003)
    if gob:
        for s in (-1,1):
            ear=[(s*.218,head+.104,.047),(s*.355,head+.158,.02),(s*.572,head+.203,-.046),
                 (s*.448,head+.079,.018),(s*.326,head-.017,.073),(s*.247,head-.026,.079)]
            m.plate("goblin",ear,.072,.021)
            m.plate("ear",[(s*.296,head+.092,.073),(s*.489,head+.162,.007),(s*.388,head+.066,.067),
                    (s*.302,head+.025,.089)],.012,.008)
            m.loft("ivory",[(s*.112,head-.137,.214,.023,.022),(s*.118,head-.096,.242,.027,.026),
                   (s*.103,head-.056,.253,.003,.003)],9)
    elif human:
        for s in (-1,1):
            m.oval("skin",(s*.263,head-.002,.008),(.060,.085,.054))
            m.oval("blush",(s*.295,head,.037),(.022,.048,.027))

    if kind == "raider":
        ring("hair",[(head+.125,-.035,.275,.211),(head+.164,-.049,.288,.221),(head+.246,-.064,.241,.180),(head+.294,-.08,.105,.075)])
        for i in range(11):
            a=i*2*pi/11
            x,z=.225*cos(a),-.045+.16*sin(a)
            m.loft("hair" if i%3 else "hairlight",[(x,head+.134,z,.086,.069),(x+.035,head+.244,z-.023,.091,.072),
                   (x+.1,head+.329+(i%3)*.029,z-.10,.005,.004)],8)
    if trader:
        # A swept cap with a rolled brim, brass pin and a pale feather.
        ring("red",[(head+.132,-.035,.293,.224),(head+.185,-.047,.33,.256),
             (head+.245,-.055,.316,.225),(head+.299,-.065,.239,.156),(head+.327,-.08,.12,.087)],18)
        m.loft("red",[(.017,head+.183,-.049,.29,.227),(.069,head+.266,-.058,.278,.210),
               (.125,head+.317,-.079,.165,.116),(.164,head+.338,-.08,.058,.043)],16)
        m.band("grain",0,head+.139,-.009,.296,.237,.069)
        crest("gold",-.113,head+.148,.207,.047,.045)
        m.ribbon("linenlight",[(-.18,head+.18,0),(-.25,head+.29,-.025),(-.32,head+.40,-.08),(-.35,head+.46,-.11)],
                 [.012,.054,.035,.001],.016)
        for s in (-1,1):
            m.oval("beard",(s*.144,head-.12,.228),(.083,.068,.055),10)
            m.ribbon("linen",[(s*.036,head-.055,.296),(s*.105,head-.072,.293),(s*.16,head-.107,.247)],
                     [.032,.039,.017],.024)
            m.ribbon("beard",[(s*.034,head-.07,.255),(s*.09,head-.087,.242),(s*.177,head-.103,.211)],
                     [.046,.047,.01],.018)
        m.loft("beard",[(0,head-.273,.17,.045,.039),(0,head-.205,.164,.137,.104),
               (0,head-.135,.13,.191,.135)],14)
        m.oval("grain",(0,.065,-.279),(.286,.293,.158))
        m.band("honey",0,.143,-.294,.287,.161,.072)
        for s in (-1,1):
            m.ribbon("brass",[(s*.139,-.17,-.371),(s*.145,.068,-.426),(s*.14,.29,-.367)],[.028]*3,.015)
        for s in (-1,1):
            m.oval("grain",(s*.31,-.105,-.283),(.103,.16,.117),12)
            m.band("oak",s*.31,-.085,-.283,.107,.126,.035,12)
            crest("brass",s*.249,-.247,.181,.028,.042)
        m.oval("linen",(0,.354,-.285),(.346,.097,.115),16)
    if mage or kind == "apprentice":
        hood="plum" if mage else "linen"
        trim="brass" if mage else "linenlight"
        m.oval(hood,(0,head+.105,-.092),(.314,.267,.209),18)
        # Aperture traces the face, leaving the eyes and nose in front of the hood.
        for s in (-1,1):
            path=[(s*.224,head-.152,.098),(s*.283,head-.023,.108),(s*.265,head+.125,.144),
                  (s*.186,head+.224,.167),(s*.061,head+.265,.165),(0,head+.281,.157)]
            m.ribbon(hood,path,[.082,.080,.069,.067,.062,.046],.045)
            m.ribbon(trim,[(x*.91,y-.012,z+.04) for x,y,z in path],[.014]*6,.012)
        if mage:
            crest("gold",0,.253,.222,.084,.079)
            crest("violet",0,.253,.24,.056,.054)
        else:
            for i in range(5):
                x=(i-2)*.063
                m.ribbon("hairlight" if i%2 else "hair",[(x+.025,head+.202,.139),(x,head+.149,.202),
                         (x-.046,head+.105+(i%2)*.019,.223)],[.041,.052,.005],.016)
            for s in (-1,1):
                m.ribbon("hair",[(s*.235,head+.108,.094),(s*.224,head-.008,.159),(s*.267,head-.139,.138)],
                         [.047,.066,.013],.031)
    if giant:
        for s in (-1,1):
            m.loft("crust",[(s*.264,-.382,.109,.058,.056),(s*.254,-.187,.125,.146,.102),
                   (s*.207,-.068,.12,.147,.113)],8)
            m.loft("obsidian",[(s*.188,.012,.108,.091,.105),(s*.224,.142,.113,.197,.128),
                   (s*.267,.261,.081,.144,.131),(s*.186,.321,.03,.102,.087)],10)
            crest("obsidian",s*.141,head+.008,.19,.137,.121)
            m.plate("sun",[(s*.049,head+.007,.247),(s*.208,head+.063,.221),(s*.164,head-.003,.242),
                            (s*.074,head-.032,.249)],.017,.001)
            crest("crust",s*.164,.095,.205,.177,.146)
            crest("basalt",s*.185,.145,.238,.152,.12)
        crest("lava",0,.081,.245,.095,.10)
        crest("sun",0,.09,.258,.039,.058)
        m.loft("lava",[(0,head+.087,0,.228,.165),(-.031,head+.196,0,.237,.161),(.018,head+.30,-.008,.188,.126),
               (.111,head+.397,-.016,.139,.082),(.201,head+.46,-.02,.103,.063),(.266,head+.57,-.02,.01,.008)],12)
        m.loft("sun",[(0,head+.147,.115,.132,.051),(-.015,head+.229,.153,.124,.044),(.056,head+.303,.12,.088,.037),
               (.141,head+.37,.09,.052,.03),(.184,head+.457,.06,.003,.003)],11)
        for i in range(9):
            a=i*2*pi/9
            x,z=.205*cos(a),.13*sin(a)
            h=.18+.10*(i%3)
            m.loft("lava",[(x,head+.12,z,.093,.084),(x+.046,head+.23,z,.085,.068),
                   (x+.097,head+.20+h*.5,z-.021,.048,.038),(x+.161,head+.19+h,z-.045,.003,.002)],9)
            m.loft("fire",[(x+.028,head+.21,z+.039,.055,.039),(x+.07,head+.28,z+.023,.042,.028),
                   (x+.136,head+.16+h,z,.003,.003)],8)

    for s in (-1,1):
        m.part="ArmL" if s<0 else "ArmR"
        x=s*.375
        sleeve="linen" if human else coat
        m.loft(sleeve,[(x+s*.04,-.047,0,.099,.097),(x+s*.027,.007,0,.126,.117),
              (x+s*.012,.144,-.003,.143,.131),(x,.22,-.008,.126,.116),(x,.267,0,.083,.087)],14)
        m.band("linenlight" if human else "grain",x+s*.04,-.013,0,.129,.12,.071)
        m.loft(skin,[(x+s*.068,-.27,.052,.081,.083),(x+s*.06,-.213,.037,.097,.102),
               (x+s*.048,-.086,.012,.099,.098),(x+s*.037,-.007,.007,.094,.092)],14)
        if not human or trader:
            m.band("basalt" if giant else "leather",x+s*.06,-.161,.026,.113,.108,.15)
            m.band("lava" if giant else "grain",x+s*.068,-.219,.034,.109,.105,.027)
            m.band("crust" if giant else "iron",x+s*.049,-.1,.018,.117,.113,.024)
        m.oval(skin,(x+s*.066,-.305,.067),(.10,.104,.089),14)
        for digit in range(3):
            m.oval(skin,(x+s*(.017+.046*digit),-.349,.098),(.032,.059,.041),8)
        m.oval(skin,(x-s*.014,-.296,.136),(.040,.062,.044))
        if giant or kind=="raider" and s==1:
            m.loft("crust" if giant else "iron",[(x,.111,.003,.144,.145),(x,.147,.003,.174,.161),
                   (x,.237,0,.173,.149),(x,.278,0,.112,.108),(x,.30,0,.049,.053)],12)
            for j in range(3):
                xx=x+s*(.044+j*.046)
                m.loft("lava" if giant else "steel",[(xx,.212,.066-j*.05,.051,.04),
                        (xx+s*.052,.292,.066-j*.05,.034,.03),(xx+s*.087,.37+j*.018,.05-j*.05,.003,.003)],7)
        if giant:
            m.loft("lava",[(x+s*.067,-.439,.074,.061,.059),(x+s*.076,-.348,.077,.134,.115),
                   (x+s*.059,-.227,.045,.123,.112)],11)
            m.loft("obsidian",[(x+s*.061,-.302,.057,.129,.126),(x+s*.069,-.219,.054,.174,.144),
                   (x+s*.06,-.119,.03,.155,.139),(x+s*.047,-.079,.025,.112,.105)],11)
            m.loft("basalt",[(x,.15,.0,.207,.171),(x+s*.078,.251,-.006,.203,.168),
                   (x+s*.155,.358,-.025,.113,.101),(x+s*.234,.449,-.054,.005,.003)],8)
        if mage and s==1:
            xw=x+.09
            m.loft("grain",[(xw,-.89,.13,.036,.039),(xw,-.54,.132,.049,.05),(xw+.017,-.21,.128,.037,.041),
                   (xw+.015,.12,.141,.046,.044),(xw-.012,.39,.133,.04,.043)],10)
            for side in (-1,1):
                m.ribbon("grain",[(xw,.36,.139),(xw+side*.11,.48,.137),(xw+side*.095,.61,.137),
                         (xw+side*.045,.69,.143)],[.042,.04,.025,.005],.037)
            m.loft("violet",[(xw,.422,.142,.005,.005),(xw,.54,.145,.072,.064),(xw,.642,.141,.006,.006)],7)
        m.part="LegL" if s<0 else "LegR"
        x=s*.164
        m.loft("basalt" if giant else "oak",[(x,-.78,.004,.102,.111),(x,-.66,-.006,.118,.124),
               (x,-.543,.004,.107,.112),(x,-.461,.016,.136,.136),(x,-.32,0,.146,.153),(x,-.242,0,.136,.139)],14)
        m.loft("obsidian" if giant else "leather",[(x,-.968,.069,.138,.210),(x,-.94,.075,.145,.216),
               (x,-.886,.083,.141,.208),(x,-.83,.046,.127,.167),(x,-.783,.005,.114,.123),
               (x,-.696,0,.123,.119)],14)
        m.band("crust" if giant else "grain",x,-.721,.003,.134,.132,.075)
        if giant:
            crest("lava",x,-.543,.127,.095,.096)
            crest("basalt",x,-.5,.15,.106,.09)
    m.save(name)

