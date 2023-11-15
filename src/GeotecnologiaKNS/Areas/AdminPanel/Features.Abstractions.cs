namespace GeotecnologiaKNS.Infra;

[AttributeUsage(AttributeTargets.Property)]
public class NonFeatureAttribute : Attribute { }

public interface IFeature { }