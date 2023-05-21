using System;
using System.Collections.Generic;

namespace FuziotDB
{
    [System.AttributeUsage(System.AttributeTargets.Field, Inherited = false, AllowMultiple = true)]
    public sealed class DBSerializeAttribute : Attribute
    {
        private static Dictionary<Type, TranslatorBase> translators = new Dictionary<Type, TranslatorBase>();

        private ASCIIS alias;
        private int length;
        private bool hasSize;
        private TranslatorBase translator;

        public ASCIIS Alias => alias;
        public int Length => length;
        public bool HasSize => hasSize;
        public TranslatorBase Translator => translator;

        public DBSerializeAttribute(string alias = "")
        {
            this.alias = new ASCIIS(alias);
            this.length = 0;
            hasSize = false;
        }

        public DBSerializeAttribute(int size)
        {
            this.alias = new ASCIIS("");
            this.length = size;
            hasSize = true;
        }

        public DBSerializeAttribute(string alias, int length)
        {
            this.alias = new ASCIIS(alias);
            this.length = length;
            hasSize = true;
        }

        public DBSerializeAttribute(string alias, Type fixedTranslator)
        {
            if(!(fixedTranslator.IsAssignableTo(typeof(TranslatorBase))))
                throw new Exception("You must provide an object that inherit FixedTranslator<T>.");

            if(fixedTranslator.GetConstructor(Type.EmptyTypes) == null)
                throw new Exception(string.Concat("Translator '", fixedTranslator.FullName, "' must have a parameterless constructor."));

            TranslatorBase translator;

            if(!translators.TryGetValue(fixedTranslator, out translator))
            {
                translator = (TranslatorBase)Activator.CreateInstance(fixedTranslator);
                translators.Add(fixedTranslator, translator);
            }

            this.alias = new ASCIIS(alias);
            this.length = translator.Size + 1;
            this.translator = translator;
            hasSize = false;
        }

        public DBSerializeAttribute(string alias, int length, Type flexibleTranslator)
        {
            if(!(flexibleTranslator.IsAssignableTo(typeof(TranslatorBase))))
                throw new Exception("You must provide an object that inherit FlexibleTranslator<T>.");

            if(flexibleTranslator.GetConstructor(Type.EmptyTypes) == null)
                throw new Exception(string.Concat("Translator '", flexibleTranslator.FullName, "' must have a parameterless constructor."));

            TranslatorBase translator;

            if(!translators.TryGetValue(flexibleTranslator, out translator))
            {
                translator = (TranslatorBase)Activator.CreateInstance(flexibleTranslator);
                translators.Add(flexibleTranslator, translator);
            }

            this.alias = new ASCIIS(alias);
            this.length = length;
            this.translator = translator;
            hasSize = true;
        }
    }
}