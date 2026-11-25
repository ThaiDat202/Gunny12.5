package horse
{
   import com.pickgliss.ui.LayerManager;
   import ddt._56$70;
   import ddt.events.PkgEvent;
   import ddt.manager.LanguageMgr;
   import ddt.manager.PlayerManager;
   import ddt.manager.SocketManager;
   import ddt.manager.StateManager;
   import ddt.manager.TimeManager;
   import flash.events.Event;
   import flash.events.IEventDispatcher;
   import flash.geom.Point;
   import flash.utils.Dictionary;
   import horse.analyzer.HorsePicCherishAnalyzer;
   import horse.analyzer.HorseSkillDataAnalyzer;
   import horse.analyzer.HorseSkillElementDataAnalyzer;
   import horse.analyzer.HorseSkillGetDataAnalyzer;
   import horse.analyzer.HorseTemplateDataAnalyzer;
   import horse.data.HorseEvent;
   import horse.data.HorsePicCherishVo;
   import horse.data.HorseSkillExpVo;
   import horse.data.HorseSkillGetVo;
   import horse.data.HorseSkillVo;
   import horse.data.HorseTemplateVo;
   import quest._199$21;
   import road7th.comm.PackageIn;
   import road7th.data.DictionaryData;
   import templateData.TemplateUpGradDataVo;
   import trainer.view.NewHandContainer;
   
   public class _139$25 extends _56$70
   {
      
      public static const GUILD_QUEST_ID:int = 568;
      
      public static const QILING_MAX_GRADE:int = 50;
      
      public static const UP_HORSE_SKILL:String = "upHorseSkill";
      
      public static const CHANGE_HORSE:String = "horseChangeHorse";
      
      public static const CHANGE_HORSE_BYPICCHERISH:String = "changeHorseByPicCherish";
      
      public static const HORSE_PIC_QILING:String = "horsepicqiling";
      
      public static const UP_HORSE_STEP_1:String = "horseUpHorseStep1";
      
      public static const UP_HORSE_STEP_2:String = "horseUpHorseStep2";
      
      public static const CHANGE_SKILL:String = "changeSkill";
      
      public static const UP_SKILL:String = "horseUpSkill";
      
      public static const TAKE_UP_DOWN_SKILL:String = "horseTakeUpDownSkill";
      
      public static const PRE_NEXT_EFFECT:String = "horsePreNextEffect";
      
      public static const REFRESH_CUR_EFFECT:String = "horseRefreshCurEffect";
      
      public static const GUIDE_6_TO_7:String = "horseGuide6To7";
      
      public static const SHOWNEWSKILLVIEW:String = "showNewSkillView";
      
      public static const HORSE_OPENVIEW:String = "horseOpenView";
      
      public static const HORSE_HIDEVIEW:String = "horseHideView";
      
      private static var _instance:_139$25;
      
      public static const MountSitList:Array = [6,7,8,9,118,119,130,137,140,151,152,163,210,214,231,251];
      
      public static const MountStandShakeList:Array = [4,5,6,7,8,9,103,137,143];
      
      public static const MountSaddleShakeList:Array = [9,137];
      
      public static const KeepMovingMountsArr:Array = [117];
      
      public var isHasLevelUp:Boolean = false;
      
      public var maxLevel:int = 89;
      
      private var _curUseHorse:int = 0;
      
      private var _curLevel:int = 0;
      
      private var _curExp:int = 0;
      
      private var _tempExp:int = 0;
      
      private var _curHasSkillList:Vector.<HorseSkillExpVo> = new Vector.<HorseSkillExpVo>();
      
      private var _curUseSkillList:DictionaryData = new DictionaryData();
      
      private var _curHasBattleSkillList:Vector.<HorseSkillExpVo> = new Vector.<HorseSkillExpVo>();
      
      private var _curUseBattleSkillList:DictionaryData = new DictionaryData();
      
      private var _horseTemplateList:Vector.<HorseTemplateVo>;
      
      private var _horseSkillGetList:DictionaryData;
      
      private var _horseSkillGetIdList:DictionaryData;
      
      private var _horseSkillGetSortArray:Array;
      
      private var _horseSkillList:DictionaryData;
      
      private var _horseSkillElementList:DictionaryData;
      
      private var _horsePicCherishList:Vector.<HorsePicCherishVo>;
      
      public var updateCherishPropertyFunc:Function;
      
      public var isSkipFromBagView:Boolean;
      
      public var skipHorsePicCherishId:int;
      
      private var _horsePicQiLingUpgradeData:Dictionary;
      
      public var isCanSelected:Boolean;
      
      public var selectedType:int;
      
      public var isNeedPlayGetNewSkillCartoon:Boolean;
      
      public var isUpFloatCartoonComplete:Boolean;
      
      private var _pValueData:DictionaryData = new DictionaryData();
      
      private var _pTxtList:Array;
      
      public function _139$25(param1:IEventDispatcher = null)
      {
         super(param1);
      }
      
      public static function get instance() : _139$25
      {
         if(_instance == null)
         {
            _instance = new _139$25();
         }
         return _instance;
      }
      
      public function isSkillHasEquip(param1:int) : Boolean
      {
         for each(var _loc2_:int in _curUseSkillList)
         {
            if(_loc2_ == param1)
            {
               return true;
            }
         }
         return false;
      }
      
      public function isEqualSkillHasEquip(param1:int) : Boolean
      {
         for each(var _loc2_:int in _curUseBattleSkillList)
         {
            if(_loc2_ == param1)
            {
               return true;
            }
         }
         return false;
      }
      
      public function get curUseSkillList() : DictionaryData
      {
         return _curUseSkillList;
      }
      
      public function get takeUpSkillPlace() : int
      {
         if(!_curUseSkillList.hasKey(1))
         {
            return 1;
         }
         if(!_curUseSkillList.hasKey(2))
         {
            return 2;
         }
         if(!_curUseSkillList.hasKey(3))
         {
            return 3;
         }
         return 0;
      }
      
      public function get takeUpEqualSkillPlace() : int
      {
         if(!_curUseBattleSkillList[1] > 0)
         {
            return 1;
         }
         if(!_curUseBattleSkillList[2] > 0)
         {
            return 2;
         }
         if(!_curUseBattleSkillList[3] > 0)
         {
            return 3;
         }
         return 0;
      }
      
      public function get horseSkillGetArray() : Array
      {
         return _horseSkillGetList.list;
      }
      
      public function get curHasSkillList() : Vector.<HorseSkillExpVo>
      {
         return _curHasSkillList;
      }
      
      public function get curExp() : int
      {
         return _curExp;
      }
      
      public function get curHasBattleSkillList() : Vector.<HorseSkillExpVo>
      {
         return _curHasBattleSkillList;
      }
      
      public function get curUseBattleSkillList() : DictionaryData
      {
         return _curUseBattleSkillList;
      }
      
      public function get tempExp() : int
      {
         return _tempExp;
      }
      
      public function get curLevel() : int
      {
         return _curLevel;
      }
      
      public function get curUseHorse() : int
      {
         return _curUseHorse;
      }
      
      public function get curHorseTemplateInfo() : HorseTemplateVo
      {
         return _horseTemplateList[_curLevel];
      }
      
      public function get nextHorseTemplateInfo() : HorseTemplateVo
      {
         if(_curLevel >= maxLevel)
         {
            return null;
         }
         return _horseTemplateList[_curLevel + 1];
      }
      
      public function getHorseTemplateInfoByLevel(param1:int) : HorseTemplateVo
      {
         if(param1 < 0 || param1 > maxLevel)
         {
            return null;
         }
         return _horseTemplateList[param1];
      }
      
      public function getHorseSkillGetInfoById(param1:int) : HorseSkillGetVo
      {
         return _horseSkillGetIdList[param1];
      }
      
      public function getHorseSkillName(param1:int, param2:int) : String
      {
         var _loc4_:HorseSkillGetVo = null;
         for each(var _loc3_:HorseSkillGetVo in _horseSkillGetIdList)
         {
            if(_loc3_.Type == param1 && _loc3_.Level == param2)
            {
               _loc4_ = _loc3_;
               break;
            }
         }
         return _horseSkillList[_loc4_.SkillID].Name;
      }
      
      public function getHorseSkillInfoById(param1:int) : HorseSkillVo
      {
         return _horseSkillList[param1];
      }
      
      public function getLevelBySkillId(param1:int) : int
      {
         var _loc2_:int = -1;
         for each(var _loc3_:HorseTemplateVo in _horseTemplateList)
         {
            if(_loc3_.SkillID == param1)
            {
               _loc2_ = _loc3_.Grade;
               break;
            }
         }
         return _loc2_;
      }
      
      public function horseTemplateDataSetup(param1:HorseTemplateDataAnalyzer) : void
      {
         _horseTemplateList = param1.horseTemplateList;
      }
      
      public function horseSkillGetDataSetup(param1:HorseSkillGetDataAnalyzer) : void
      {
         var _loc12_:int = 0;
         var _loc8_:int = 0;
         var _loc6_:int = 0;
         var _loc9_:int = 0;
         _horseSkillGetIdList = param1.horseSkillGetIdList;
         var _loc5_:DictionaryData = param1.horseSkillGetList;
         var _loc2_:Array = [];
         var _loc3_:int = int(_horseTemplateList.length);
         _loc12_ = 0;
         while(_loc12_ < _loc3_)
         {
            if(_horseTemplateList[_loc12_].SkillID > 0)
            {
               _loc2_.push(_horseTemplateList[_loc12_].SkillID);
            }
            _loc12_++;
         }
         var _loc4_:Array = [];
         var _loc11_:int = int(_loc2_.length);
         _loc8_ = 0;
         while(_loc8_ < _loc11_)
         {
            _loc6_ = int(_loc2_[_loc8_]);
            for(var _loc10_:String in _loc5_)
            {
               if(_loc5_[_loc10_][0].SkillID == _loc6_)
               {
                  _loc4_.push(_loc5_[_loc10_][0].Type);
                  break;
               }
            }
            _loc8_++;
         }
         var _loc7_:int = int(_loc4_.length);
         _horseSkillGetList = new DictionaryData();
         _loc9_ = 0;
         while(_loc9_ < _loc7_)
         {
            _horseSkillGetList.add(_loc4_[_loc9_],_loc5_[_loc4_[_loc9_]]);
            _loc9_++;
         }
      }
      
      public function horseSkillDataSetup(param1:HorseSkillDataAnalyzer) : void
      {
         _horseSkillList = param1.horseSkillList;
      }
      
      public function horseSkillElementDataSetup(param1:HorseSkillElementDataAnalyzer) : void
      {
         _horseSkillElementList = param1.horseSkillElementList;
      }
      
      public function setup() : void
      {
         SocketManager.Instance.addEventListener(PkgEvent.format(260),pkgHandler);
      }
      
      private function pkgHandler(param1:PkgEvent) : void
      {
         var _loc3_:PackageIn = param1.pkg;
         var _loc2_:int = _loc3_.readByte();
         switch(_loc2_)
         {
            case 1:
               initAllData(_loc3_);
               break;
            case 2:
               changeHorse(_loc3_);
               break;
            case 3:
               upHorse(_loc3_);
               break;
            case 4:
               getSkillHandler(_loc3_);
               break;
            case 5:
               upSkillHandler(_loc3_);
               break;
            case 6:
               takeUpDownSkillHandler(_loc3_);
               break;
            case 8:
               picCherishInfo(_loc3_);
               break;
            case 9:
               initBattleHorseSkill(_loc3_);
               break;
            case 10:
               takeUpDownEqualSkill(_loc3_);
               break;
            case 7:
               activeHandler(_loc3_);
               break;
            case 32:
               qilingHandler(_loc3_);
         }
      }
      
      private function takeUpDownSkillHandler(param1:PackageIn) : void
      {
         var _loc3_:int = param1.readInt();
         var _loc2_:int = param1.readInt();
         if(_loc2_ > 0)
         {
            _curUseSkillList.add(_loc2_,_loc3_);
         }
         else
         {
            for(var _loc4_:String in _curUseSkillList)
            {
               if(_curUseSkillList[_loc4_] == _loc3_)
               {
                  _curUseSkillList.remove(_loc4_);
                  break;
               }
            }
         }
         dispatchEvent(new Event("horseTakeUpDownSkill"));
      }
      
      private function upSkillHandler(param1:PackageIn) : void
      {
         var _loc6_:int = param1.readInt();
         var _loc4_:int = param1.readInt();
         var _loc2_:int = param1.readInt();
         for each(var _loc5_:HorseSkillExpVo in _curHasSkillList)
         {
            if(_loc5_.skillId == _loc6_)
            {
               _loc5_.skillId = _loc4_;
               _loc5_.exp = _loc2_;
               break;
            }
         }
         for(var _loc3_:String in _curUseSkillList)
         {
            if(_curUseSkillList[_loc3_] == _loc6_)
            {
               _curUseSkillList.add(_loc3_,_loc4_);
               break;
            }
         }
         dispatchEvent(new Event("horseUpSkill"));
         dispatchEvent(new Event("horseTakeUpDownSkill"));
      }
      
      private function getSkillHandler(param1:PackageIn) : void
      {
         var _loc2_:HorseSkillExpVo = new HorseSkillExpVo();
         _loc2_.skillId = param1.readInt();
         _loc2_.exp = param1.readInt();
         _curHasSkillList.push(_loc2_);
         isNeedPlayGetNewSkillCartoon = true;
         dispatchEvent(new Event("showNewSkillView"));
      }
      
      private function upHorse(param1:PackageIn) : void
      {
         isHasLevelUp = param1.readBoolean();
         _curLevel = param1.readInt();
         _curExp = param1.readInt();
         isUpFloatCartoonComplete = false;
         dispatchEvent(new Event("horseUpHorseStep1"));
      }
      
      private function changeHorse(param1:PackageIn) : void
      {
         _curUseHorse = param1.readInt();
         updateHorse();
         if(_curUseHorse > 100)
         {
            dispatchEvent(new Event("changeHorseByPicCherish"));
         }
         PlayerManager.Instance.Self.MountsType = _curUseHorse;
      }
      
      private function updateHorse() : void
      {
         for(var _loc1_:String in PlayerManager.Instance.Self.horsePicCherishDic)
         {
            PlayerManager.Instance.Self.horsePicCherishDic[_loc1_].isUsed = int(_loc1_) == _curUseHorse;
         }
         dispatchEvent(new HorseEvent("horseChangeHorse"));
      }
      
      private function initAllData(param1:PackageIn) : void
      {
         var _loc5_:int = 0;
         var _loc4_:HorseSkillExpVo = null;
         var _loc3_:int = 0;
         _curUseHorse = param1.readInt();
         _curLevel = param1.readInt();
         _curExp = param1.readInt();
         param1.readInt();
         var _loc2_:int = param1.readInt();
         _curHasSkillList = new Vector.<HorseSkillExpVo>();
         _curUseSkillList = new DictionaryData();
         _loc5_ = 0;
         while(_loc5_ < _loc2_)
         {
            _loc4_ = new HorseSkillExpVo();
            _loc4_.skillId = param1.readInt();
            _loc4_.exp = param1.readInt();
            _curHasSkillList.push(_loc4_);
            _loc3_ = param1.readInt();
            if(_loc3_ > 0)
            {
               _curUseSkillList.add(_loc3_,_loc4_.skillId);
            }
            _loc5_++;
         }
      }
      
      public function getHorseLevelByExp(param1:int) : int
      {
         var _loc3_:int = 0;
         var _loc2_:int = 0;
         if(_horseTemplateList[_horseTemplateList.length - 1].Experience <= param1)
         {
            return _horseTemplateList.length - 1;
         }
         _loc3_ = 0;
         while(_loc3_ < _horseTemplateList.length)
         {
            if(_horseTemplateList[_loc3_].Experience > param1)
            {
               _loc2_ = _loc3_ - 1;
               break;
            }
            _loc3_++;
         }
         return _loc2_;
      }
      
      private function picCherishInfo(param1:PackageIn) : void
      {
         var _loc12_:int = 0;
         var _loc2_:int = 0;
         var _loc6_:Boolean = false;
         var _loc10_:Date = null;
         var _loc3_:int = 0;
         var _loc9_:Number = NaN;
         var _loc5_:int = 0;
         var _loc4_:Number = NaN;
         var _loc11_:Boolean = false;
         var _loc8_:Object = null;
         PlayerManager.Instance.Self.horsePicCherishDic.clear();
         var _loc7_:int = param1.readInt();
         _loc12_ = 0;
         while(_loc12_ < _loc7_)
         {
            _loc2_ = param1.readInt();
            _loc6_ = param1.readInt() == 1;
            _loc10_ = param1.readDate();
            _loc3_ = param1.readInt();
            _loc9_ = _loc3_ * 86400000 + _loc10_.time;
            _loc5_ = param1.readInt();
            _loc4_ = 86400000 * _loc3_ + _loc10_.getTime();
            _loc11_ = _loc3_ == 0 || _loc4_ > TimeManager.Instance.Now().getTime();
            if(!_loc11_ && _loc6_)
            {
               SocketManager.Instance.out.sendHorseChangeHorse(0);
            }
            _loc8_ = {
               "id":_loc2_,
               "isUsed":_loc6_,
               "beginDate":_loc10_,
               "valid":_loc3_,
               "endTime":_loc9_,
               "qiLingGrade":_loc5_,
               "isValid":_loc11_
            };
            PlayerManager.Instance.Self.horsePicCherishDic.add(_loc2_,_loc8_);
            _loc12_++;
         }
      }
      
      private function activeHandler(param1:PackageIn) : void
      {
         var _loc2_:int = param1.readInt();
         var _loc5_:Boolean = param1.readInt() == 1;
         var _loc8_:Date = param1.readDate();
         var _loc3_:int = param1.readInt();
         var _loc7_:Number = _loc3_ * 86400000 + _loc8_.time;
         var _loc4_:int = param1.readInt();
         var _loc6_:Object = {
            "id":_loc2_,
            "isUsed":_loc5_,
            "beginDate":_loc8_,
            "valid":_loc3_,
            "endTime":_loc7_,
            "qiLingGrade":_loc4_,
            "isValid":true
         };
         PlayerManager.Instance.Self.horsePicCherishDic.add(_loc2_,_loc6_);
         if(updateCherishPropertyFunc != null)
         {
            updateCherishPropertyFunc();
         }
         dispatchEvent(new Event("changeHorseByPicCherish"));
      }
      
      private function qilingHandler(param1:PackageIn) : void
      {
         var _loc3_:int = param1.readInt();
         var _loc2_:int = param1.readInt();
         var _loc4_:int = param1.readInt();
         PlayerManager.Instance.Self.horsePicCherishDic[_loc3_].qiLingGrade = _loc2_;
         dispatchEvent(new Event("horsepicqiling"));
      }
      
      public function horsePicCherishDataSetup(param1:HorsePicCherishAnalyzer) : void
      {
         _horsePicCherishList = param1.horsePicCherishList;
      }
      
      public function getHorsePicCherishData() : Vector.<HorsePicCherishVo>
      {
         return _horsePicCherishList;
      }
      
      public function getHorsePicVoByTempID(param1:int) : HorsePicCherishVo
      {
         for each(var _loc2_:HorsePicCherishVo in _horsePicCherishList)
         {
            if(_loc2_.TemplateId == param1)
            {
               return _loc2_;
            }
         }
         return null;
      }
      
      public function getHorsePicVoByID(param1:int) : HorsePicCherishVo
      {
         for each(var _loc2_:HorsePicCherishVo in _horsePicCherishList)
         {
            if(_loc2_.ID == param1)
            {
               return _loc2_;
            }
         }
         return null;
      }
      
      public function getHorsePropertyValueByID(param1:int) : String
      {
         var _loc4_:Array = null;
         var _loc5_:int = 0;
         _pTxtList ||= LanguageMgr.GetTranslation("horse.horsePicCherish.peopertyTxt").split(",");
         var _loc3_:String = _pValueData[param1] || "";
         if(!_loc3_ || _loc3_ == "")
         {
            for each(var _loc2_:HorsePicCherishVo in _horsePicCherishList)
            {
               if(_loc2_.ID == param1)
               {
                  _loc4_ = [_loc2_.AddHurt,_loc2_.AddGuard,_loc2_.AddBlood,_loc2_.MagicAttack,_loc2_.MagicDefence];
                  _loc5_ = 0;
                  while(_loc5_ < _loc4_.length)
                  {
                     if(_loc4_[_loc5_] != 0)
                     {
                        _loc3_ += _pTxtList[_loc5_] + _loc4_[_loc5_] + "\n";
                     }
                     _loc5_++;
                  }
                  _pValueData.add(param1,_loc3_);
               }
            }
         }
         return _loc3_;
      }
      
      override protected function start() : void
      {
         dispatchEvent(new Event("horseOpenView"));
      }
      
      public function closeFrame() : void
      {
         dispatchEvent(new Event("horseHideView"));
      }
      
      public function get horseSkillGetIdList() : DictionaryData
      {
         return _horseSkillGetIdList;
      }
      
      public function get horseTemplateList() : Vector.<HorseTemplateVo>
      {
         return _horseTemplateList;
      }
      
      public function getIsSit(param1:int) : Boolean
      {
         return _139$25.MountSitList.indexOf(param1) != -1;
      }
      
      public function getIsShakeRide(param1:int) : Boolean
      {
         return _139$25.MountStandShakeList.indexOf(param1) != -1;
      }
      
      public function getIsKeepMovingRide(param1:int) : Boolean
      {
         return _139$25.KeepMovingMountsArr.indexOf(param1) != -1;
      }
      
      public function getIsSaddleShake(param1:int) : Boolean
      {
         return _139$25.MountSaddleShakeList.indexOf(param1) != -1;
      }
      
      private function initBattleHorseSkill(param1:PackageIn) : void
      {
         var _loc6_:int = 0;
         var _loc3_:HorseSkillExpVo = null;
         var _loc7_:int = 0;
         var _loc4_:int = 0;
         _curHasBattleSkillList = new Vector.<HorseSkillExpVo>();
         var _loc5_:int = param1.readInt();
         _loc6_ = 0;
         while(_loc6_ < _loc5_)
         {
            _loc3_ = new HorseSkillExpVo();
            _loc3_.skillId = param1.readInt();
            _curHasBattleSkillList.push(_loc3_);
            _loc6_++;
         }
         _curUseBattleSkillList = new DictionaryData();
         var _loc2_:int = param1.readInt();
         _loc7_ = 0;
         while(_loc7_ < _loc2_)
         {
            _loc4_ = param1.readInt();
            _curUseBattleSkillList.add(_loc7_ + 1,_loc4_);
            _loc7_++;
         }
      }
      
      private function takeUpDownEqualSkill(param1:PackageIn) : void
      {
         var _loc2_:int = param1.readInt();
         var _loc3_:int = param1.readInt();
         _curUseBattleSkillList.add(_loc3_,_loc2_);
         dispatchEvent(new Event("horseTakeUpDownSkill"));
      }
      
      public function set horsePicQiLingUpgradeData(param1:Dictionary) : void
      {
         _horsePicQiLingUpgradeData = param1;
      }
      
      public function getHorsePicQiLingInfoByGrade(param1:int) : TemplateUpGradDataVo
      {
         return _horsePicQiLingUpgradeData[param1];
      }
      
      public function getQiLingBorderIndexByGrade(param1:int) : int
      {
         if(param1 < 10)
         {
            return 1;
         }
         if(param1 < 20)
         {
            return 2;
         }
         if(param1 < 30)
         {
            return 3;
         }
         if(param1 < 40)
         {
            return 4;
         }
         if(param1 < 50)
         {
            return 5;
         }
         return 6;
      }
      
      public function get curHasSkillListArray() : Array
      {
         var _loc2_:int = 0;
         var _loc1_:Array = [];
         _loc2_ = 0;
         while(_loc2_ < _curHasSkillList.length)
         {
            _loc1_.push(_curHasSkillList[_loc2_]);
            _loc2_++;
         }
         return _loc1_;
      }
      
      public function horseGuild1() : void
      {
         if(!PlayerManager.Instance.Self.IsWeakGuildFinish(110) && StateManager.currentStateType == "main" && _199$21.instance.isAchieved(_199$21.instance.getQuestByID(568)))
         {
            if(PlayerManager.Instance.Self.Grade == 12)
            {
               NewHandContainer.Instance.showGuideCover("circle",[805,570,30],2);
               NewHandContainer.Instance.showArrow(128,0,new Point(797,471),"","",LayerManager.Instance.getLayerByType(2));
               SocketManager.Instance.out.syncWeakStep(110);
            }
         }
      }
      
      public function horseGuild2() : void
      {
         if(PlayerManager.Instance.Self.IsWeakGuildFinish(110) && !PlayerManager.Instance.Self.IsWeakGuildFinish(111))
         {
            NewHandContainer.Instance.clearAll();
            NewHandContainer.Instance.hideGuideCover();
            NewHandContainer.Instance.showGuideCover("rect",[245,75,90,30],2);
            NewHandContainer.Instance.showArrow(128,-180,new Point(287,165),"","",LayerManager.Instance.getLayerByType(2));
            SocketManager.Instance.out.syncWeakStep(111);
         }
      }
      
      public function horseGuild3() : void
      {
         if(PlayerManager.Instance.Self.IsWeakGuildFinish(111) && !PlayerManager.Instance.Self.IsWeakGuildFinish(112))
         {
            NewHandContainer.Instance.clearAll();
            NewHandContainer.Instance.hideGuideCover();
            NewHandContainer.Instance.showGuideCover("rect",[148,254,45,45],2);
            NewHandContainer.Instance.showArrow(128,-180,new Point(170,372),"asset.trainer.txtHorseSkillTip","trainer.txtHorseSkillTipPos",LayerManager.Instance.getLayerByType(2));
         }
      }
   }
}

