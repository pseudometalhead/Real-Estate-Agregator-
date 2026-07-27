using EstateAggregator.Utilities;

namespace EstateAggregator.Tests;

public class DescriptionCleanerTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Clean_NullEmptyOrWhitespace_ReturnsInputUnchanged(string? description)
    {
        Assert.Equal(description, DescriptionCleaner.Clean(description));
    }

    [Fact]
    public void Clean_NoBoilerplate_ReturnsTextUnchanged()
    {
        var description = "Apartamento T2 com varanda, cozinha equipada e elevador. Excelente localização junto ao centro.";

        Assert.Equal(description, DescriptionCleaner.Clean(description));
    }

    // Real example (trimmed), verified live: an ImoVirtual listing whose
    // description came through with literal "<br>" tags instead of line
    // breaks — ImoVirtualScraper's own StripDescriptionHtml only catches this
    // for rows scraped after that fix existed, so Clean() needs its own net.
    [Fact]
    public void Clean_LiteralBrTags_BecomeLineBreaksNotVisibleMarkup()
    {
        var description = "Apartamento T1 em construção, piso 1<br><br>ACABAMENTOS EXTERIORES<br>Guardas das varandas em ferro galvanizado";

        var cleaned = DescriptionCleaner.Clean(description);

        Assert.DoesNotContain("<br>", cleaned);
        Assert.Contains("piso 1\n\nACABAMENTOS EXTERIORES\nGuardas das varandas em ferro galvanizado", cleaned);
    }

    [Fact]
    public void Clean_OtherStrayHtmlTags_AreStrippedNotShown()
    {
        var description = "Apartamento <strong>totalmente remodelado</strong>, com <b>vista mar</b>.";

        var cleaned = DescriptionCleaner.Clean(description);

        Assert.Equal("Apartamento totalmente remodelado, com vista mar.", cleaned);
    }

    // Real example (trimmed), verified live: a Zome-branded listing appends
    // a "3 razões para comprar com a Zome" testimonial section followed by
    // a broker-share notice, none of it about the actual property.
    [Fact]
    public void Clean_ZomeStyleMarketingSection_TruncatesFromTriggerOnward()
    {
        var description =
            "Descubra este T2 completamente transformado, com 99,4 m² de área.\n\n" +
            "Um imóvel novo, bem construído e pronto a personalizar. Marque já a sua visita.\n\n" +
            "3 razões para comprar com a Zome\n\n" +
            "+ acompanhamento\n\n" +
            "Com uma preparação e experiência única no mercado imobiliário...\n\n" +
            "Notas:\n\n" +
            "1. Caso seja um consultor imobiliário, este imóvel está disponível para partilha de negócio. Não hesite em apresentar aos seus clientes.";

        var cleaned = DescriptionCleaner.Clean(description);

        Assert.Contains("Descubra este T2", cleaned);
        Assert.DoesNotContain("razões para comprar", cleaned);
        Assert.DoesNotContain("acompanhamento", cleaned);
        Assert.DoesNotContain("partilha de negócio", cleaned);
    }

    [Theory]
    [InlineData("Contacte para mais informações e marcação de visita.")]
    [InlineData("Entre em contacto hoje mesmo para assegurar o seu próximo passo seguro no mercado imobiliário.")]
    [InlineData("Agende já a sua visita.")]
    [InlineData("Marque a sua visita.")]
    [InlineData("Não hesite em contactar-nos para mais informações.")]
    public void Clean_StandaloneCtaLine_IsRemoved(string ctaLine)
    {
        var description = $"Apartamento T2 com vista desafogada e boa exposição solar.\n\n{ctaLine}";

        var cleaned = DescriptionCleaner.Clean(description);

        Assert.Contains("vista desafogada", cleaned);
        Assert.DoesNotContain(ctaLine, cleaned);
    }

    [Fact]
    public void Clean_TrailingReferenceCodeLine_IsRemoved()
    {
        var description = "Penthouse T5 com terraço exclusivo e localização premium no centro do Porto.\n\nref:APA_1765.";

        var cleaned = DescriptionCleaner.Clean(description);

        Assert.Contains("Penthouse T5", cleaned);
        Assert.DoesNotContain("ref:APA_1765", cleaned);
    }

    [Fact]
    public void Clean_CtaPhraseEmbeddedInFactualSentence_KeepsTheLine()
    {
        // "Contacte" appears here, but the line as a whole is real
        // information (a factual instruction), not a bare sales CTA — only
        // a line that IS a CTA should be dropped, not any line mentioning
        // the word.
        var description = "Junto à Câmara Municipal. Para agendamentos de obras, contacte os serviços municipais em horário de expediente, disponível para consulta de plantas aprovadas.";

        var cleaned = DescriptionCleaner.Clean(description);

        Assert.Equal(description, cleaned);
    }

    [Fact]
    public void Clean_PureBoilerplateDescription_FallsBackToOriginalRatherThanEmpty()
    {
        var description = "3 razões para comprar com a Zome";

        var cleaned = DescriptionCleaner.Clean(description);

        Assert.Equal(description, cleaned);
    }

    // Real example (trimmed), verified live on a CustoJusto listing:
    // HtmlAgilityPack's HtmlEntity.DeEntitize mangles numeric HTML entities
    // for emoji (codepoints above the Basic Multilingual Plane) into a bare
    // "#128205;" — losing only the leading "&" — before Clean() ever sees
    // the text. 128205 is the decimal codepoint for 📍 (round pushpin).
    [Fact]
    public void Clean_BrokenAstralEntityAtLineStart_RestoresTheEmoji()
    {
        var description = "#128205; Localização central, com acesso facilitado a comércio, serviços, escolas e transportes.";

        var cleaned = DescriptionCleaner.Clean(description);

        Assert.Equal("📍 Localização central, com acesso facilitado a comércio, serviços, escolas e transportes.", cleaned);
    }

    [Fact]
    public void Clean_MultipleBrokenAstralEntitiesOnSeparateLines_RestoresEach()
    {
        // 129000 = 🟨 (large yellow square), 128296 = 🔨 (hammer),
        // 128176 = 💰 (money bag).
        var description = "#129000; Principais características:\n\n#128296; Conclusão da obra prevista em 24 meses.\n\n#128176; Preços a partir de 187.200 eur";

        var cleaned = DescriptionCleaner.Clean(description);

        Assert.StartsWith("\U0001F7E8 Principais características:", cleaned);
        Assert.Contains("\U0001F528 Conclusão da obra prevista em 24 meses.", cleaned);
        Assert.Contains("\U0001F4B0 Preços a partir de 187.200 eur", cleaned);
    }

    [Fact]
    public void Clean_NumberFollowedBySemicolonMidSentence_IsNotTreatedAsBrokenEntity()
    {
        // The broken-entity pattern only matches at the START of a line —
        // a number-then-semicolon appearing mid-sentence is just punctuation
        // and must be left alone.
        var description = "Preço: 128000; sujeito a negociação.";

        var cleaned = DescriptionCleaner.Clean(description);

        Assert.Equal(description, cleaned);
    }

    // Real example (trimmed), verified live: both RapidAPI's Idealista feed
    // and ImoVirtual's own served HTML sometimes already contain literal
    // U+FFFD characters in place of an accented letter — data already lost
    // upstream, before this app ever fetches it. Clean() can't recover the
    // original letter, but should at least not display the "�" glyph.
    [Fact]
    public void Clean_StrayReplacementCharacter_IsStrippedNotShown()
    {
        var description = "Descubra o seu ref�gio urbano no cora��o de Vilar de Paraiso.";

        var cleaned = DescriptionCleaner.Clean(description);

        Assert.DoesNotContain("�", cleaned);
        Assert.Equal("Descubra o seu refgio urbano no corao de Vilar de Paraiso.", cleaned);
    }
}
