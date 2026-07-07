package com.barcodeplatform.pda.scanner

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent

class DataWedgeReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context?, intent: Intent?) {
        if (intent == null) return

        val barcode = intent.getStringExtra("com.symbol.datawedge.data_string") // Zebra
            ?: intent.getStringExtra("com.honeywell.decode.intent.extras.DATA") // Honeywell
            ?: intent.getStringExtra("com.datalogic.decode.intent.extras.barcode_string") // Datalogic
            ?: intent.getStringExtra("data")
            ?: return

        val labelType = intent.getStringExtra("com.symbol.datawedge.label_type") // Zebra
            ?: intent.getStringExtra("com.honeywell.decode.intent.extras.CODE_ID") // Honeywell
            ?: intent.getStringExtra("com.datalogic.decode.intent.extras.barcode_type") // Datalogic

        val symbology = mapToSymbology(labelType)

        val scanIntent = Intent(ACTION_SCAN).apply {
            putExtra(EXTRA_BARCODE, barcode)
            putExtra(EXTRA_SYMBOLOGY, symbology)
            setPackage(context?.packageName)
        }
        context?.sendBroadcast(scanIntent)
    }

    private fun mapToSymbology(labelType: String?): String {
        if (labelType == null) return "Unknown"
        val type = labelType.lowercase()
        return when {
            type.contains("upc") -> "Upc"
            type.contains("ean") -> "Ean"
            type.contains("code39") -> "Code39"
            type.contains("code128") -> "Code128"
            type.contains("i2of5") || type.contains("itf") -> "Itf"
            type.contains("code93") -> "Code93"
            type.contains("codabar") -> "Codabar"
            type.contains("databar") -> "Gs1DataBar"
            type.contains("msi") -> "MsiPlessey"
            type.contains("codablock") -> "Codablock"
            type.contains("qr") -> "QrCode"
            type.contains("datamatrix") -> "DataMatrix"
            type.contains("pdf417") -> "Pdf417"
            type.contains("aztec") -> "Aztec"
            type.contains("maxicode") -> "MaxiCode"
            type.contains("hanxin") -> "HanXin"
            type.contains("dotcode") -> "DotCode"
            type.contains("gs1-128") || type.contains("gs1_128") -> "Gs1128"
            else -> "Unknown"
        }
    }

    companion object {
        const val ACTION_SCAN = "com.barcodeplatform.pda.SCAN"
        const val EXTRA_BARCODE = "barcode"
        const val EXTRA_SYMBOLOGY = "symbology"
    }
}
