//
// Commons.Xml.Relaxng.RelaxngPattern.cs
//
// Author:
//	Atsushi Enomoto <ginga@kit.hi-ho.ne.jp>
//
// 2003 Atsushi Enomoto "No rights reserved."
//

using System;
using System.Collections;
using System.IO;
using System.Xml;
using Commons.Xml.Relaxng.Derivative;

namespace Commons.Xml.Relaxng
{
	#region Common abstract
	public abstract class RelaxngElementBase
	{
		bool isCompiled;
		int lineNumber, linePosition;
		string baseUri;

		internal bool IsCompiled {
			get { return isCompiled; }
			set { isCompiled = value; }
		}

		public int LineNumber {
			get { return lineNumber; }
			set { lineNumber = value; }
		}

		public int LinePosition {
			get { return linePosition; }
			set { linePosition = value; }
		}

		public string BaseUri {
			get { return baseUri; }
			set { baseUri = value; }
		}

		public abstract void Write (XmlWriter write);
	}

	public abstract class RelaxngSingleContentPattern : RelaxngPattern
	{
		private RelaxngPatternList patterns = new RelaxngPatternList ();

		public RelaxngPatternList Patterns {
			get { return patterns; }
		}

		internal RdpPattern makeSingle (RelaxngGrammar g)
		{
			// Flatten patterns into RdpGroup. See 4.12.
			if (patterns.Count == 0)
				throw new RelaxngException ("No pattern contents.");
			RdpPattern p = ((RelaxngPattern) patterns [0]).Compile (g);
			if (patterns.Count == 1)
				return p;
			for (int i=1; i<patterns.Count; i++) {
				p = new RdpGroup (p,
					((RelaxngPattern) patterns [i]).Compile (g));
			}
			return p;
		}

		internal override void CheckConstraints () 
		{
			foreach (RelaxngPattern p in Patterns)
				p.CheckConstraints ();
		}
	}

	public abstract class RelaxngBinaryContentPattern : RelaxngPattern
	{
		private RelaxngPatternList patterns = new RelaxngPatternList ();

		public RelaxngPatternList Patterns {
			get { return patterns; }
		}

		internal RdpPattern makeBinary (RelaxngGrammar g)
		{
			// Flatten patterns. See 4.12.
			if (patterns.Count == 0)
				throw new RelaxngException ("No pattern contents.");

			RdpPattern p = ((RelaxngPattern) patterns [0]).Compile (g);
			if (patterns.Count == 1)
				return p;

			for (int i=1; i<patterns.Count; i++) {
				RdpPattern cp =
					((RelaxngPattern) patterns [i]).Compile (g);
				switch (this.PatternType) {
				case RelaxngPatternType.Choice:
					p = new RdpChoice (p, cp);
					break;
				case RelaxngPatternType.Group:
					p = new RdpGroup (p, cp);
					break;
				case RelaxngPatternType.Interleave:
					p = new RdpInterleave (p, cp);
					break;
				}
			}

			return p;
		}

		internal override void CheckConstraints () 
		{
			foreach (RelaxngPattern p in Patterns)
				p.CheckConstraints ();
		}
	}
	#endregion

	#region Grammatical elements
	public interface IGrammarContent
	{
	}

	public class RelaxngStart : RelaxngElementBase, IGrammarContent
	{
		RelaxngPattern p;
		string combine;

		public RelaxngStart ()
		{
		}

		public string Combine {
			get { return combine; }
			set { combine = value; }
		}

		public RelaxngPattern Pattern {
			get { return p; }
			set { p = value; }
		}

		public override void Write (XmlWriter writer)
		{
			writer.WriteStartElement ("", "start", RelaxngGrammar.NamespaceURI);
			if (combine != null)
				writer.WriteAttributeString ("combine", combine);
			p.Write (writer);
			writer.WriteEndElement ();
		}

		internal RdpPattern Compile (RelaxngGrammar grammar)
		{
			return p.Compile (grammar);
		}
	}

	public class RelaxngDefine : RelaxngElementBase, IGrammarContent
	{
		string name;
		private RelaxngPatternList patterns = new RelaxngPatternList ();
		string combine;

		public RelaxngDefine ()
		{
		}

		public RelaxngPatternList Patterns {
			get { return patterns; }
		}

		public string Combine {
			get { return combine; }
			set { combine = value; }
		}

		public string Name {
			get { return name; }
			set { name = value; }
		}

		public override void Write (XmlWriter writer)
		{
			writer.WriteStartElement ("", "define", RelaxngGrammar.NamespaceURI);
			writer.WriteAttributeString ("name", name);
			if (combine != null)
				writer.WriteAttributeString ("combine", combine);
			foreach (RelaxngPattern p in Patterns)
				p.Write (writer);
			writer.WriteEndElement ();
		}

		internal RdpPattern Compile (RelaxngGrammar grammar)
		{
			return makeSingle (grammar);
		}

		private RdpPattern makeSingle (RelaxngGrammar g)
		{
			// Flatten patterns into RdpGroup. See 4.12.
			if (patterns.Count == 0)
				throw new RelaxngException ("No pattern contents.");
			RdpPattern p = ((RelaxngPattern) patterns [0]).Compile (g);
			if (patterns.Count == 1)
				return p;
			for (int i=1; i<patterns.Count; i++) {
				p = new RdpGroup (p,
					((RelaxngPattern) patterns [i]).Compile (g));
			}
			return p;
		}
	}

	public class RelaxngInclude : RelaxngElementBase, IGrammarContent
	{
		string href;
		RelaxngGrammarContentList starts = new RelaxngGrammarContentList ();
		RelaxngGrammarContentList defines = new RelaxngGrammarContentList ();
		RelaxngGrammarContentList divs = new RelaxngGrammarContentList ();
		string ns;

		public RelaxngInclude ()
		{
		}

		public string Href {
			get { return href; }
			set { href = value; }
		}

		public RelaxngGrammarContentList Starts {
			get { return starts; }
		}

		public RelaxngGrammarContentList Defines {
			get { return defines; }
		}

		public RelaxngGrammarContentList Divs {
			get { return divs; }
		}

		public string NSContext {
			get { return ns; }
			set { ns = value; }
		}

		public override void Write (XmlWriter writer)
		{
			writer.WriteStartElement ("", "include", RelaxngGrammar.NamespaceURI);
			writer.WriteAttributeString ("href", href);
			foreach (RelaxngStart start in Starts)
				start.Write (writer);
			foreach (RelaxngDefine define in Defines)
				define.Write (writer);
			foreach (RelaxngDiv div in Divs)
				div.Write (writer);
			writer.WriteEndElement ();
		}

		// compile into div
		internal RelaxngDiv Compile (RelaxngGrammar grammar)
		{
			grammar.CheckIncludeRecursion (Href);
			grammar.IncludedUris.Add (Href, Href);
			string url = Util.ResolveUri (BaseUri, href);
			XmlTextReader xtr = new XmlTextReader (url);
			RelaxngGrammar g = null;
			try {
				RelaxngReader r = new RelaxngReader (xtr, ns);
				r.MoveToContent ();
				g = r.ReadPattern () as RelaxngGrammar;
			} finally {
				xtr.Close ();
			}
			if (g == null)
				throw new RelaxngException ("Included syntax must start with \"grammar\" element.");
			g.DataProvider = grammar.Provider;

			// process recursive inclusions.
			foreach (RelaxngInclude inc in g.Includes)
				g.Divs.Add (inc.Compile (grammar));

			// process this own div children.
			// each div subelements are also compiled.
			foreach (RelaxngDiv cdiv in divs)
				cdiv.Compile (g);
			foreach (RelaxngDiv cdiv in g.Divs)
				cdiv.Compile (g);

			// replace redifinitions into div.
			// starts.
			if (this.Starts.Count > 0 && g.Starts.Count == 0)
				throw new RelaxngException ("When the included grammar does not contain start components, this include component must not contain start components.");
			RelaxngGrammarContentList appliedStarts = (this.starts.Count > 0) ?
				this.starts : g.Starts;

			RelaxngDiv div = new RelaxngDiv ();
			div.BaseUri = this.BaseUri;
			div.LinePosition = this.LinePosition;
			div.LineNumber = this.LineNumber;

			foreach (RelaxngStart start in appliedStarts)
				div.Starts.Add (start);

			// defines.
			Hashtable overrides = new Hashtable ();
			Hashtable originalDefs = new Hashtable ();
			foreach (RelaxngDefine def in defines) {
				overrides.Add (def.Name, def.Name);
				div.Defines.Add (def);
			}
			foreach (RelaxngDefine def in g.Defines) {
				originalDefs.Add (def.Name, def.Name);
				if (overrides [def.Name] == null)
					div.Defines.Add (def);
				// else discard.
			}
			foreach (string name in overrides.Values)
				if (!originalDefs.Contains (name))
					throw new RelaxngException ("The include component must not contain define components whose name does not appear in the included grammar component.");

			grammar.IncludedUris.Remove (Href);
			return div;
		}
	}

	public class RelaxngDiv : RelaxngElementBase, IGrammarContent
	{
		RelaxngGrammarContentList starts = new RelaxngGrammarContentList ();
		RelaxngGrammarContentList defines = new RelaxngGrammarContentList ();
		RelaxngGrammarContentList includes = new RelaxngGrammarContentList ();
		RelaxngGrammarContentList divs = new RelaxngGrammarContentList ();

		public RelaxngDiv ()
		{
		}

		public RelaxngGrammarContentList Starts {
			get { return starts; }
		}

		public RelaxngGrammarContentList Defines {
			get { return defines; }
		}

		public RelaxngGrammarContentList Includes {
			get { return includes; }
		}

		public RelaxngGrammarContentList Divs {
			get { return divs; }
		}

		public override void Write (XmlWriter writer)
		{
			writer.WriteStartElement ("", "div", RelaxngGrammar.NamespaceURI);
			foreach (RelaxngStart start in Starts)
				start.Write (writer);
			foreach (RelaxngDefine define in Defines)
				define.Write (writer);
			foreach (RelaxngInclude include in Includes)
				include.Write (writer);
			foreach (RelaxngDiv div in Divs)
				div.Write (writer);
			writer.WriteEndElement ();
		}

		internal void Compile (RelaxngGrammar grammar)
		{
			foreach (RelaxngDiv div in divs)
				div.Compile (grammar);
			foreach (RelaxngInclude inc in includes)
				inc.Compile (grammar).Compile (grammar); // compile compiled divs
			foreach (RelaxngStart start in starts)
				grammar.Starts.Add (start);
			foreach (RelaxngDefine define in defines)
				grammar.Defines.Add (define);
		}
	}
	#endregion

	#region RelaxngPatterns
	public abstract class RelaxngPattern : RelaxngElementBase
	{
		// static

		public static RelaxngPattern Read (XmlReader xmlReader)
		{
			return Read (xmlReader, null);
		}

		public static RelaxngPattern Read (XmlReader xmlReader, RelaxngDatatypeProvider provider)
		{
			RelaxngReader r = new RelaxngReader (xmlReader, null);
			if (r.ReadState == ReadState.Initial)
				r.Read ();
			r.MoveToContent ();
			RelaxngPattern p = r.ReadPattern ();
			p.DataProvider = provider;
			return p;
		}

		// Private Fields
		RdpPattern startRelaxngPattern;
		RelaxngDatatypeProvider provider;

		// Public
		public abstract RelaxngPatternType PatternType { get; }
		public RelaxngDatatypeProvider DataProvider {
			get {
				return provider; 
			}
			set { 
				provider = value; 
			}
		}

		public void Compile ()
		{
			RelaxngGrammar g = null;
			if (this is RelaxngGrammar)
				g = (RelaxngGrammar) this;
			else {
				g = new RelaxngGrammar ();
				g.BaseUri = this.BaseUri;
				g.LineNumber = this.LineNumber;
				g.LinePosition = this.LinePosition;
				RelaxngStart st = new RelaxngStart ();
				st.BaseUri = this.BaseUri;
				st.LineNumber = this.LineNumber;
				st.LinePosition = this.LinePosition;
				st.Pattern = this;
				g.Starts.Add (st);
				g.Provider = provider;
			}
			startRelaxngPattern = g.Compile (null);
			this.IsCompiled = true;
		}


		// Internal
		internal abstract void CheckConstraints ();

		protected RelaxngPattern () 
		{
		}

		internal abstract RdpPattern Compile (RelaxngGrammar grammar);

		internal RdpPattern StartPattern {
			get { return startRelaxngPattern; }
		}
	}

	public class RelaxngPatternList : CollectionBase
	{
		public RelaxngPatternList ()
		{
		}

		public void Add (RelaxngPattern p)
		{
			List.Add (p);
		}

		public RelaxngPattern this [int i] {
			get { return this.List [i] as RelaxngPattern; }
			set { this.List [i] = value; }
		}

		public void Insert (int pos, RelaxngPattern p)
		{
			List.Insert (pos, p);
		}

		public void Remove (RelaxngPattern p)
		{
			List.Remove (p);
		}
	}

	public class RelaxngGrammarContentList : CollectionBase
	{
		public RelaxngGrammarContentList ()
		{
		}

		public void Add (IGrammarContent p)
		{
			List.Add (p);
		}

		public IGrammarContent this [int i] {
			get { return this.List [i] as IGrammarContent; }
			set { this.List [i] = value; }
		}

		public void Insert (int pos, IGrammarContent p)
		{
			List.Insert (pos, p);
		}

		public void Remove (IGrammarContent p)
		{
			List.Remove (p);
		}
	}

	// strict to say, it's not a pattern ;)
	public class RelaxngNotAllowed : RelaxngPattern
	{
		public RelaxngNotAllowed () 
		{
		}

		public override RelaxngPatternType PatternType {
			get { return RelaxngPatternType.NotAllowed; }
		}

		public override void Write (XmlWriter writer)
		{
			writer.WriteStartElement ("", "notAllowed", RelaxngGrammar.NamespaceURI);
			writer.WriteEndElement ();
		}

		internal override RdpPattern Compile (RelaxngGrammar grammar)
		{
			return RdpNotAllowed.Instance;
		}

		internal override void CheckConstraints () 
		{
			// nothing to check
		}
	}

	public class RelaxngEmpty : RelaxngPattern
	{
		public RelaxngEmpty ()
		{
		}

		public override RelaxngPatternType PatternType {
			get { return RelaxngPatternType.Empty; }
		}

		public override void Write (XmlWriter writer)
		{
			writer.WriteStartElement ("", "empty", RelaxngGrammar.NamespaceURI);
			writer.WriteEndElement ();
		}

		internal override RdpPattern Compile (RelaxngGrammar grammar)
		{
			return RdpEmpty.Instance;
		}

		internal override void CheckConstraints () 
		{
			// nothing to check
		}
	}

	public class RelaxngText : RelaxngPattern
	{
		public RelaxngText () 
		{
		}

		public override RelaxngPatternType PatternType {
			get { return RelaxngPatternType.Text; }
		}

		public override void Write (XmlWriter writer)
		{
			writer.WriteStartElement ("", "text", RelaxngGrammar.NamespaceURI);
			writer.WriteEndElement ();
		}

		internal override RdpPattern Compile (RelaxngGrammar grammar)
		{
			return RdpText.Instance;
		}

		internal override void CheckConstraints () 
		{
			// nothing to check
		}
	}

	public abstract class RelaxngDataSupport : RelaxngPattern
	{
		string type;
		string datatypeLibrary;

		public string Type {
			get { return type; }
			set { type = value; }
		}

		public string DatatypeLibrary {
			get { return datatypeLibrary; }
			set { datatypeLibrary = value; }
		}

		internal void CheckDatatypeName ()
		{
			// Data type name check is done in RdpData(Except) derivative creation.
		}
	}

	public class RelaxngData : RelaxngDataSupport
	{
		RelaxngParamList paramList = new RelaxngParamList ();
		RelaxngExcept except;

		public RelaxngData ()
		{
		}

		public override RelaxngPatternType PatternType {
			get { return RelaxngPatternType.Data; }
		}

		public RelaxngParamList ParamList {
			get { return paramList; }
		}

		public RelaxngExcept Except {
			get { return except; }
			set { except = value; }
		}

		public override void Write (XmlWriter writer)
		{
			writer.WriteStartElement ("", "data", RelaxngGrammar.NamespaceURI);
			if (DatatypeLibrary != null && DatatypeLibrary != String.Empty)
				writer.WriteAttributeString ("xmlns", "data", "http://www.w3.org/2000/xmlns/", DatatypeLibrary);
			writer.WriteStartAttribute ("type", String.Empty);
			writer.WriteQualifiedName (Type, DatatypeLibrary);
			writer.WriteEndAttribute ();

			foreach (RelaxngParam p in ParamList)
				p.Write (writer);

			if (Except != null)
				Except.Write (writer);

			writer.WriteEndElement ();
		}

		internal override RdpPattern Compile (RelaxngGrammar grammar)
		{
//			RdpParamList rdpl = new RdpParamList ();
//			foreach (RelaxngParam prm in this.paramList)
//				rdpl.Add (prm.Compile (grammar));
			RdpPattern p = null;
			if (this.except != null) {
				if (except.Patterns.Count == 0)
					throw new RelaxngException ("data except pattern have no children.");
				p = except.Patterns [0].Compile (grammar);
				for (int i=1; i<except.Patterns.Count; i++)
					p = new RdpChoice (p,
						except.Patterns [i].Compile (grammar));
			}

			IsCompiled = true;
			if (this.except != null)
				return new RdpDataExcept (new RdpDatatype (DatatypeLibrary, Type, ParamList, grammar.Provider), p);
			else
				return new RdpData (new RdpDatatype (DatatypeLibrary, Type, ParamList, grammar.Provider));
		}

		internal override void CheckConstraints () 
		{
			CheckDatatypeName ();
		}
	}

	public class RelaxngValue : RelaxngDataSupport
	{
		string value;

		public override RelaxngPatternType PatternType {
			get { return RelaxngPatternType.Value; }
		}

		public string Value {
			get { return value; }
			set { this.value = value; }
		}

		public override void Write (XmlWriter writer)
		{
			writer.WriteStartElement ("", "value", RelaxngGrammar.NamespaceURI);
			if (Type != null) {
				writer.WriteStartAttribute ("type", String.Empty);
				if (DatatypeLibrary != null && DatatypeLibrary != String.Empty)
					writer.WriteAttributeString ("xmlns", "data", "http://www.w3.org/2000/xmlns/", DatatypeLibrary);
				writer.WriteQualifiedName (Type, DatatypeLibrary);
				writer.WriteEndAttribute ();
			}
			writer.WriteString (Value);
			writer.WriteEndElement ();
		}

		internal override RdpPattern Compile (RelaxngGrammar grammar)
		{
			IsCompiled = true;
			return new RdpValue (new RdpDatatype (DatatypeLibrary,
				Type, null, grammar.Provider), value);
		}

		internal override void CheckConstraints () 
		{
			CheckDatatypeName ();
		}
	}

	public class RelaxngList : RelaxngSingleContentPattern
	{
		internal RelaxngList ()
		{
		}

		public override RelaxngPatternType PatternType {
			get { return RelaxngPatternType.List; }
		}

		public override void Write (XmlWriter writer)
		{
			writer.WriteStartElement ("", "list", RelaxngGrammar.NamespaceURI);
			foreach (RelaxngPattern p in Patterns)
				p.Write (writer);
			writer.WriteEndElement ();
		}

		internal override RdpPattern Compile (RelaxngGrammar grammar)
		{

			IsCompiled = true;
			return new RdpList (makeSingle (grammar));
		}

		internal override void CheckConstraints () 
		{
			// nothing to check
		}
	}

	public class RelaxngElement : RelaxngSingleContentPattern
	{
		RelaxngNameClass nc;

		public RelaxngElement ()
		{
		}

		public RelaxngNameClass NameClass {
			get { return nc; }
			set { nc = value; }
		}

		public override RelaxngPatternType PatternType {
			get { return RelaxngPatternType.Element; }
		}

		public override void Write (XmlWriter writer)
		{
			writer.WriteStartElement ("", "element", RelaxngGrammar.NamespaceURI);
			nc.Write (writer);
			foreach (RelaxngPattern p in Patterns)
				p.Write (writer);
			writer.WriteEndElement ();
		}

		internal override RdpPattern Compile (RelaxngGrammar grammar)
		{
			return new RdpElement (
				nc.Compile (grammar), this.makeSingle (grammar));
		}

		internal override void CheckConstraints () 
		{
			NameClass.CheckConstraints (false, false);

			foreach (RelaxngPattern p in Patterns)
				p.CheckConstraints ();
		}
	}

	public class RelaxngAttribute : RelaxngPattern
	{
		RelaxngNameClass nc;
		RelaxngPattern p;

		public RelaxngAttribute ()
		{
		}

		public RelaxngPattern Pattern {
			get { return p; }
			set { p = value; }
		}

		public RelaxngNameClass NameClass {
			get { return nc; }
			set { nc = value; }
		}

		public override RelaxngPatternType PatternType {
			get { return RelaxngPatternType.Attribute; }
		}

		public override void Write (XmlWriter writer)
		{
			writer.WriteStartElement ("", "attribute", RelaxngGrammar.NamespaceURI);
			nc.Write (writer);
			if (p != null)
				p.Write (writer);
			writer.WriteEndElement ();
		}

		private void checkInvalidAttrNameClass (RdpNameClass nc)
		{
			string xmlnsNS = "http://www.w3.org/2000/xmlns";
			RdpNameClassChoice choice = nc as RdpNameClassChoice;
			if (choice != null) {
				checkInvalidAttrNameClass (choice.LValue);
				checkInvalidAttrNameClass (choice.RValue);
				return;
			}
			RdpAnyNameExcept except = nc as RdpAnyNameExcept;
			if (except != null) {
				checkInvalidAttrNameClass (except.ExceptNameClass);
				return;
			}
			if (nc is RdpAnyName)
				return;

			RdpName n = nc as RdpName;
			if (n != null) {
				if (n.NamespaceURI == xmlnsNS)
					throw new RelaxngException ("cannot specify \"" + xmlnsNS + "\" for name of attribute.");
				if (n.LocalName == "xmlns" && n.NamespaceURI == "")
					throw new RelaxngException ("cannot specify \"xmlns\" inside empty ns context.");
			} else {
				RdpNsName nn = nc as RdpNsName;
				if (nn.NamespaceURI == "http://www.w3.org/2000/xmlns")
					throw new RelaxngException ("cannot specify \"" + xmlnsNS + "\" for name of attribute.");
				RdpNsNameExcept x = nc as RdpNsNameExcept;
				if (x != null)
					checkInvalidAttrNameClass (x.ExceptNameClass);
			}
		}

		internal override RdpPattern Compile (RelaxngGrammar grammar)
		{
			IsCompiled = true;
			RdpNameClass cnc = nc.Compile (grammar);
			this.checkInvalidAttrNameClass (cnc);

			return new RdpAttribute (cnc,
				(p != null) ?
					p.Compile (grammar) :
					RdpText.Instance);
		}

		internal override void CheckConstraints () 
		{
			NameClass.CheckConstraints (false, false);

			if (p != null)
				p.CheckConstraints ();
		}
	}

	internal class RdpUnresolvedRef : RdpPattern
	{
		string name;
//		bool parentRef;
		RelaxngGrammar targetGrammar;
		RdpPattern referencedPattern;

		public RdpUnresolvedRef (string name, RelaxngGrammar g)
		{
			this.name = name;
//			this.parentRef = parentRef;
			targetGrammar = g;
		}

		public string Name {
			get { return name; }
			set { name = value; }
		}

		public RdpPattern RefPattern {
			get { return referencedPattern; }
			set { referencedPattern = value; }
		}

//		public bool IsParentRef {
//			get { return parentRef; }
//		}

		public RelaxngGrammar TargetGrammar {
			get { return targetGrammar; }
		}

		public override RelaxngPatternType PatternType {
			get { return RelaxngPatternType.Ref; }
		}

		public override RdpContentType ContentType {
#if REPLACE_IN_ADVANCE
			get { throw new InvalidOperationException (); }
#else
			get { return RdpContentType.Empty; }
#endif
		}


		public override bool Nullable {
			get {
				throw new InvalidOperationException ();
			}
		}

		internal override RdpPattern ExpandRef (Hashtable defs)
		{
			RdpPattern target = (RdpPattern) defs [this.name];
			if (target == null)
				throw new RelaxngException ("Target definition " + name + " not found.");
			return target.ExpandRef (defs);
		}

		internal override void MarkReachableDefs () 
		{
			TargetGrammar.MarkReacheableDefine (this.name);
		}

		internal override void CheckConstraints (bool attribute, bool oneOrMore, bool oneOrMoreGroup, bool oneOrMoreInterleave, bool list, bool dataExcept) 
		{
//			throw new InvalidOperationException ();
		}

		internal override bool ContainsText ()
		{
			return false;
//			throw new InvalidOperationException ();
		}
	}

	public class RelaxngRef : RelaxngPattern
	{
		string name;

		public RelaxngRef ()
		{
		}

		public string Name {
			get { return name; }
			set { name = value; }
		}

		public override RelaxngPatternType PatternType {
			get { return RelaxngPatternType.Ref; }
		}

		public override void Write (XmlWriter writer)
		{
			writer.WriteStartElement ("", "ref", RelaxngGrammar.NamespaceURI);
			writer.WriteAttributeString ("name", name);
			writer.WriteEndElement ();
		}

		internal override RdpPattern Compile (RelaxngGrammar grammar)
		{
			// Important!! This compile method only generates stub.
			IsCompiled = false;
			return new RdpUnresolvedRef (name, grammar);
		}

		internal override void CheckConstraints () 
		{
			// nothing to check
		}
	}

	public class RelaxngParentRef : RelaxngPattern
	{
		string name;

		public RelaxngParentRef ()
		{
		}

		public string Name {
			get { return name; }
			set { name = value; }
		}

		public override RelaxngPatternType PatternType {
			get { return RelaxngPatternType.ParentRef; }
		}

		public override void Write (XmlWriter writer)
		{
			writer.WriteStartElement ("", "parentRef", RelaxngGrammar.NamespaceURI);
			writer.WriteAttributeString ("name", name);
			writer.WriteEndElement ();
		}

		internal override RdpPattern Compile (RelaxngGrammar grammar)
		{
			IsCompiled = false;
			return new RdpUnresolvedRef (name, grammar.ParentGrammar);
		}

		internal override void CheckConstraints () 
		{
			// nothing to check
		}
	}

	public class RelaxngExternalRef : RelaxngPattern
	{
		string href;
		string ns;

		public RelaxngExternalRef ()
		{
		}

		public string Href {
			get { return href; }
			set { href = value; }
		}

		public string NSContext {
			get { return ns; }
			set { ns = value; }
		}

		public override RelaxngPatternType PatternType {
			get { return RelaxngPatternType.ExternalRef; }
		}

		public override void Write (XmlWriter writer)
		{
			writer.WriteStartElement ("", "externalRef", RelaxngGrammar.NamespaceURI);
			writer.WriteAttributeString ("href", Href);
			writer.WriteEndElement ();
		}

		internal override RdpPattern Compile (RelaxngGrammar grammar)
		{
			grammar.CheckIncludeRecursion (Href);
			grammar.IncludedUris.Add (Href, Href);
			string uri = Util.ResolveUri (this.BaseUri, href);
			RelaxngReader r = new RelaxngReader (new XmlTextReader (uri), ns);
			r.MoveToContent ();
			RelaxngPattern p = r.ReadPattern ();
			p.DataProvider = grammar.Provider;

			RdpPattern ret = p.Compile (grammar);

			grammar.IncludedUris.Remove (Href);

			return ret;
		}

		internal override void CheckConstraints () 
		{
			// nothing to check
		}
	}

	public class RelaxngOneOrMore : RelaxngSingleContentPattern
	{
		public RelaxngOneOrMore ()
		{
		}

		public override RelaxngPatternType PatternType {
			get { return RelaxngPatternType.OneOrMore; }
		}

		public override void Write (XmlWriter writer)
		{
			writer.WriteStartElement ("", "oneOrMore", RelaxngGrammar.NamespaceURI);
			foreach (RelaxngPattern p in Patterns)
				p.Write (writer);
			writer.WriteEndElement ();
		}

		internal override RdpPattern Compile (RelaxngGrammar grammar)
		{
			IsCompiled = true;
			return new RdpOneOrMore (makeSingle (grammar));
		}
	}

	public class RelaxngZeroOrMore : RelaxngSingleContentPattern
	{
		public RelaxngZeroOrMore ()
		{
		}

		public override RelaxngPatternType PatternType {
			get { return RelaxngPatternType.ZeroOrMore; }
		}

		public override void Write (XmlWriter writer)
		{
			writer.WriteStartElement ("", "zeroOrMore", RelaxngGrammar.NamespaceURI);
			foreach (RelaxngPattern p in Patterns)
				p.Write (writer);
			writer.WriteEndElement ();
		}

		internal override RdpPattern Compile (RelaxngGrammar grammar)
		{
			IsCompiled = true;
			return new RdpChoice (
				new RdpOneOrMore (makeSingle (grammar)),
				RdpEmpty.Instance);
		}
	}

	public class RelaxngOptional : RelaxngSingleContentPattern
	{
		public RelaxngOptional ()
		{
		}

		public override RelaxngPatternType PatternType {
			get { return RelaxngPatternType.Optional; }
		}

		public override void Write (XmlWriter writer)
		{
			writer.WriteStartElement ("", "optional", RelaxngGrammar.NamespaceURI);
			foreach (RelaxngPattern p in Patterns)
				p.Write (writer);
			writer.WriteEndElement ();
		}

		internal override RdpPattern Compile (RelaxngGrammar grammar)
		{
			IsCompiled = true;
			return new RdpChoice (
				makeSingle (grammar), RdpEmpty.Instance);
		}
	}

	public class RelaxngMixed : RelaxngSingleContentPattern
	{
		public RelaxngMixed ()
		{
		}

		public override RelaxngPatternType PatternType {
			get { return RelaxngPatternType.Mixed; }
		}

		public override void Write (XmlWriter writer)
		{
			writer.WriteStartElement ("", "mixed", RelaxngGrammar.NamespaceURI);
			foreach (RelaxngPattern p in Patterns)
				p.Write (writer);
			writer.WriteEndElement ();
		}

		internal override RdpPattern Compile (RelaxngGrammar grammar)
		{
			IsCompiled = true;
			return new RdpInterleave (makeSingle (grammar), RdpText.Instance);
		}
	}

	public class RelaxngChoice : RelaxngBinaryContentPattern
	{
		public RelaxngChoice ()
		{
		}

		public override RelaxngPatternType PatternType {
			get { return RelaxngPatternType.Choice; }
		}

		public override void Write (XmlWriter writer)
		{
			writer.WriteStartElement ("", "choice", RelaxngGrammar.NamespaceURI);
			foreach (RelaxngPattern p in Patterns)
				p.Write (writer);
			writer.WriteEndElement ();
		}

		internal override RdpPattern Compile (RelaxngGrammar grammar)
		{
			IsCompiled = true;
			return makeBinary (grammar);
		}
	}

	public class RelaxngGroup : RelaxngBinaryContentPattern
	{
		public RelaxngGroup ()
		{
		}

		public override RelaxngPatternType PatternType {
			get { return RelaxngPatternType.Group; }
		}

		public override void Write (XmlWriter writer)
		{
			writer.WriteStartElement ("", "group", RelaxngGrammar.NamespaceURI);
			foreach (RelaxngPattern p in Patterns)
				p.Write (writer);
			writer.WriteEndElement ();
		}

		internal override RdpPattern Compile (RelaxngGrammar grammar)
		{
			IsCompiled = true;
			return makeBinary (grammar);
		}
	}

	public class RelaxngInterleave : RelaxngBinaryContentPattern
	{
		public RelaxngInterleave ()
		{
		}

		public override RelaxngPatternType PatternType {
			get { return RelaxngPatternType.Interleave; }
		}

		public override void Write (XmlWriter writer)
		{
			writer.WriteStartElement ("", "interleave", RelaxngGrammar.NamespaceURI);
			foreach (RelaxngPattern p in Patterns)
				p.Write (writer);
			writer.WriteEndElement ();
		}

		internal override RdpPattern Compile (RelaxngGrammar grammar)
		{
			IsCompiled = true;
			return makeBinary (grammar);
		}
	}

	public class RelaxngParam : RelaxngElementBase
	{
		string name;
		string value;

		public RelaxngParam ()
		{
		}

		public RelaxngParam (string name, string value)
		{
			this.name = name;
			this.value = value;
		}

		public string Name {
			get { return name; }
			set { name = value; }
		}

		public string Value {
			get { return value; }
			set { this.value = value; }
		}

		public override void Write (XmlWriter writer)
		{
			writer.WriteStartElement ("", "param", RelaxngGrammar.NamespaceURI);
			writer.WriteAttributeString ("name", name);
			writer.WriteString (Value);
			writer.WriteEndElement ();
		}

		internal RdpParam Compile (RelaxngGrammar grammar)
		{
			IsCompiled = true;
			return new RdpParam (name, value);
		}
	}

	public class RelaxngParamList : CollectionBase
	{
		public RelaxngParamList ()
		{
		}

		public void Add (RelaxngParam p)
		{
			List.Add (p);
		}

		public RelaxngParam this [int i] {
			get { return this.List [i] as RelaxngParam; }
			set { this.List [i] = value; }
		}

		public void Insert (int pos, RelaxngParam p)
		{
			List.Insert (pos, p);
		}

		public void Remove (RelaxngParam p)
		{
			List.Remove (p);
		}
	}

	public class RelaxngExcept : RelaxngElementBase
	{
		RelaxngPatternList patterns = new RelaxngPatternList ();

		public RelaxngExcept ()
		{
		}

		public RelaxngPatternList Patterns {
			get { return patterns; }
		}

		public override void Write (XmlWriter writer)
		{
			writer.WriteStartElement ("", "except", RelaxngGrammar.NamespaceURI);
			foreach (RelaxngPattern p in Patterns)
				p.Write (writer);
			writer.WriteEndElement ();
		}
	}

	public class RelaxngRefPattern
	{
		RelaxngPattern patternRef;
		string name;

		// When we found ref, use it.
		public RelaxngRefPattern (string name)
		{
			this.name = name;
		}

		// When we found define, use it.
		public RelaxngRefPattern (RelaxngPattern patternRef)
		{
			this.patternRef = patternRef;
		}

		public string Name {
			get { return name; }
		}

		public RelaxngPattern PatternRef {
			get { return patternRef; }
			set { patternRef = value; }
		}
	}
	#endregion
}
