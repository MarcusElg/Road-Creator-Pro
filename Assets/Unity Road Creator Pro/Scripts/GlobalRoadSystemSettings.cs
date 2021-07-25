#if UNITY_EDITOR
namespace RoadCreatorPro {
    
    // Global settings used by RoadSystemToolbarOverlay
    public static class GlobalRoadSystemSettings {
        public enum Action { None, Create, Disconnect, Connect, CreateRoad, AddPoints, DeletePoints, InsertPoints, SplitRoad }
        public static Action currentAction = Action.None;

        public static bool createStraightSegment = false;
        public static bool yLock = false;
        public static bool movePointsIndividually = false;
    }   
}
#endif
